using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using AfterApply.Application.Applications;
using AfterApply.Application.Imports;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.Persistence;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Imports;

internal sealed partial class ImportService(
    AppDbContext dbContext, ICompanyResolver companyResolver, IJobResolver jobResolver, IOptions<ImportOptions> options)
    : IImportService
{
    public async Task<ImportSummaryResponse> ImportCsvAsync(Guid userId, Stream csvStream, string fileName, long fileLength,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new CsvImportValidationException(["Sadece .csv dosyaları desteklenir."]);
        }

        if (fileLength <= 0)
        {
            throw new CsvImportValidationException(["Dosya boş."]);
        }

        if (fileLength > opts.MaxFileSizeBytes)
        {
            throw new CsvImportValidationException([$"Dosya boyutu {opts.MaxFileSizeBytes} byte sınırını aşıyor."]);
        }

        var batch = ImportBatch.Create(userId, Source.CsvImport, fileName, DateTimeOffset.UtcNow);
        dbContext.ImportBatches.Add(batch);

        var ctx = await BuildDedupContextAsync(userId, cancellationToken);
        var counts = new RowCounts();

        using var reader = new StreamReader(csvStream);
        await ProcessCsvAsync(userId, batch, ctx, reader, Source.CsvImport, resolveJob: false,
            columnMapping, counts, opts, cancellationToken);

        batch.Complete(counts.Total, counts.New, counts.Duplicate, counts.Invalid, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(batch);
    }

    public async Task<ImportSummaryResponse> ImportLinkedInZipAsync(Guid userId, Stream zipStream, string fileName,
        long fileLength, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new CsvImportValidationException(["Sadece .zip dosyaları desteklenir."]);
        }

        if (fileLength <= 0)
        {
            throw new CsvImportValidationException(["Dosya boş."]);
        }

        if (fileLength > opts.MaxZipSizeBytes)
        {
            throw new CsvImportValidationException([$"ZIP boyutu {opts.MaxZipSizeBytes} byte sınırını aşıyor."]);
        }

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        if (archive.Entries.Count > opts.MaxZipEntryCount)
        {
            throw new CsvImportValidationException([$"ZIP içindeki dosya sayısı {opts.MaxZipEntryCount} sınırını aşıyor."]);
        }

        var matchedEntries = archive.Entries.Where(e => JobApplicationsFileRegex().IsMatch(e.Name)).ToList();

        if (matchedEntries.Count == 0)
        {
            throw new CsvImportValidationException(["ZIP içinde 'Job Applications*.csv' dosyası bulunamadı."]);
        }

        foreach (var entry in matchedEntries)
        {
            if (entry.Length > opts.MaxFileSizeBytes)
            {
                throw new CsvImportValidationException([$"'{entry.FullName}' dosyası {opts.MaxFileSizeBytes} byte sınırını aşıyor."]);
            }
        }

        var batch = ImportBatch.Create(userId, Source.LinkedInImport, fileName, DateTimeOffset.UtcNow);
        dbContext.ImportBatches.Add(batch);

        var ctx = await BuildDedupContextAsync(userId, cancellationToken);
        var counts = new RowCounts();

        foreach (var entry in matchedEntries)
        {
            await using var entryStream = new LimitedStream(entry.Open(), opts.MaxFileSizeBytes);
            using var reader = new StreamReader(entryStream);

            try
            {
                await ProcessCsvAsync(userId, batch, ctx, reader, Source.LinkedInImport, resolveJob: true,
                    columnMappingOverride: null, counts, opts, cancellationToken);
            }
            catch (StreamLengthExceededException)
            {
                throw new CsvImportValidationException([$"'{entry.FullName}' dosyası açılırken boyut sınırı aşıldı."]);
            }
        }

        batch.Complete(counts.Total, counts.New, counts.Duplicate, counts.Invalid, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(batch);
    }

    public async Task<ImportSummaryResponse?> GetByIdAsync(Guid userId, Guid importId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .Include(b => b.RowErrors)
            .FirstOrDefaultAsync(b => b.Id == importId && b.UserId == userId, cancellationToken);

        return batch is null ? null : ToSummary(batch);
    }

    private async Task ProcessCsvAsync(Guid userId, ImportBatch batch, DedupContext ctx, TextReader reader,
        Source source, bool resolveJob, IReadOnlyDictionary<string, string>? columnMappingOverride,
        RowCounts counts, ImportOptions opts, CancellationToken cancellationToken)
    {
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
        {
            throw new CsvImportValidationException(["CSV dosyasında başlık satırı bulunamadı."]);
        }

        var headers = csv.HeaderRecord ?? [];
        var (mapping, mappingErrors) = CsvColumnMapper.Map(headers, columnMappingOverride);
        if (mapping is null)
        {
            throw new CsvImportValidationException(mappingErrors);
        }

        while (await csv.ReadAsync())
        {
            counts.Total++;

            if (counts.Total > opts.MaxRowCount)
            {
                throw new CsvImportValidationException([$"Satır sayısı {opts.MaxRowCount} sınırını aşıyor."]);
            }

            var rawRow = headers.ToDictionary(h => h, h => csv.GetField(h));
            var rawRowText = string.Join(", ", headers.Select(h => $"{h}={csv.GetField(h)}"));

            var outcome = await ProcessRowAsync(userId, batch, ctx, rawRow, mapping, counts.Total, rawRowText,
                source, resolveJob, cancellationToken);

            switch (outcome)
            {
                case RowOutcome.Invalid:
                    counts.Invalid++;
                    break;
                case RowOutcome.Duplicate:
                    counts.Duplicate++;
                    break;
                case RowOutcome.New:
                    counts.New++;
                    break;
            }
        }
    }

    private async Task<RowOutcome> ProcessRowAsync(Guid userId, ImportBatch batch, DedupContext ctx,
        IReadOnlyDictionary<string, string?> rawRow, ColumnMapping mapping, int rowNumber, string rawRowText,
        Source source, bool resolveJob, CancellationToken cancellationToken)
    {
        var (parsed, error) = ImportRowParser.Parse(rawRow, mapping);
        if (parsed is null)
        {
            batch.AddRowError(rowNumber, rawRowText, error!);
            dbContext.ImportRowErrors.Add(batch.RowErrors.Last());
            return RowOutcome.Invalid;
        }

        var externalId = resolveJob ? LinkedInJobIdExtractor.Extract(parsed.JobUrl) : null;

        if (externalId is not null && ctx.ExistingExternalIds.Contains((source, externalId)))
        {
            return RowOutcome.Duplicate;
        }

        if (parsed.JobUrl is not null && ctx.ExistingUrls.Contains(parsed.JobUrl))
        {
            return RowOutcome.Duplicate;
        }

        var companyId = await companyResolver.ResolveOrCreateAsync(parsed.CompanyName, cancellationToken);
        var normalizedTitle = JobTitleNormalizer.Normalize(parsed.JobTitle);
        var appliedDate = DateOnly.FromDateTime(parsed.AppliedAt.UtcDateTime);

        if (ctx.ExistingCompanyTitleDates.Contains((companyId, normalizedTitle, appliedDate)))
        {
            return RowOutcome.Duplicate;
        }

        Guid? jobId = null;
        if (resolveJob)
        {
            jobId = await jobResolver.ResolveOrCreateAsync(companyId, parsed.JobTitle, source, parsed.JobUrl,
                externalId, parsed.Location, cancellationToken);
        }

        var application = DomainApplication.Create(userId, companyId, parsed.JobTitle, parsed.JobUrl, parsed.Location,
            EmploymentType.FullTime, parsed.AppliedAt, source, notes: null, DateTimeOffset.UtcNow, jobId);

        if (parsed.Status != ApplicationStatus.Applied)
        {
            application.ChangeStatus(parsed.Status, parsed.AppliedAt, source, note: null);
        }

        dbContext.Applications.Add(application);

        if (parsed.JobUrl is not null)
        {
            ctx.ExistingUrls.Add(parsed.JobUrl);
        }

        ctx.ExistingCompanyTitleDates.Add((companyId, normalizedTitle, appliedDate));

        if (externalId is not null)
        {
            ctx.ExistingExternalIds.Add((source, externalId));
        }

        return RowOutcome.New;
    }

    private async Task<DedupContext> BuildDedupContextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await (
            from a in dbContext.Applications
            where a.UserId == userId
            join j in dbContext.Jobs on a.JobId equals j.Id into jobs
            from j in jobs.DefaultIfEmpty()
            select new
            {
                a.CompanyId, a.JobTitle, a.JobUrl, a.AppliedAt,
                JobSource = (Source?)j.Source, j.ExternalId
            }).ToListAsync(cancellationToken);

        var existingUrls = new HashSet<string>(
            existing.Where(a => !string.IsNullOrWhiteSpace(a.JobUrl)).Select(a => a.JobUrl!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var existingCompanyTitleDates = existing
            .Select(a => (a.CompanyId, NormalizedTitle: JobTitleNormalizer.Normalize(a.JobTitle), Date: DateOnly.FromDateTime(a.AppliedAt.UtcDateTime)))
            .ToHashSet();

        var existingExternalIds = existing
            .Where(a => a.JobSource is not null && a.ExternalId is not null)
            .Select(a => (a.JobSource!.Value, a.ExternalId!))
            .ToHashSet();

        return new DedupContext(existingUrls, existingCompanyTitleDates, existingExternalIds);
    }

    private static ImportSummaryResponse ToSummary(ImportBatch batch)
    {
        return new ImportSummaryResponse(
            batch.Id, batch.Source, batch.FileName, batch.TotalRecords, batch.NewApplications, batch.DuplicateRecords,
            batch.InvalidRecords, batch.CompletedAt,
            batch.RowErrors.Select(e => new ImportRowErrorResponse(e.RowNumber, e.RawRow, e.ErrorMessage)).ToList());
    }

    [GeneratedRegex(@"^Job Applications(_\d+)?\.csv$", RegexOptions.IgnoreCase)]
    private static partial Regex JobApplicationsFileRegex();

    private enum RowOutcome
    {
        Invalid,
        Duplicate,
        New
    }

    private sealed class RowCounts
    {
        public int Total;
        public int New;
        public int Duplicate;
        public int Invalid;
    }

    private sealed record DedupContext(
        HashSet<string> ExistingUrls,
        HashSet<(Guid CompanyId, string NormalizedTitle, DateOnly Date)> ExistingCompanyTitleDates,
        HashSet<(Source Source, string ExternalId)> ExistingExternalIds);
}
