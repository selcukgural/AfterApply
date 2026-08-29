using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using AfterApply.Application.Applications;
using AfterApply.Application.Imports;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.Persistence;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Imports;

internal sealed partial class ImportService(
    AppDbContext dbContext, ICompanyResolver companyResolver, IJobResolver jobResolver, IOptions<ImportOptions> options,
    IStringLocalizer<SharedStrings> localizer, IImportProgressNotifier progressNotifier)
    : IImportService
{
    // How often (in rows) processing pushes a progress update. Small enough to feel live on a
    // 5000-row import, large enough not to hammer the DB/SignalR with a save+push every row.
    private const int ProgressReportInterval = 25;

    public async Task<StagedImport> StageCsvImportAsync(Guid userId, Stream csvStream, string fileName, long fileLength,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new CsvImportValidationException([localizer["IMPORT_ONLY_CSV_SUPPORTED"]]);
        }

        if (fileLength <= 0)
        {
            throw new CsvImportValidationException([localizer["IMPORT_FILE_EMPTY"]]);
        }

        if (fileLength > opts.MaxFileSizeBytes)
        {
            throw new CsvImportValidationException([localizer["IMPORT_FILE_TOO_LARGE", opts.MaxFileSizeBytes]]);
        }

        var stagedPath = await StageFileAsync(csvStream, ".csv", cancellationToken);

        var batch = ImportBatch.Create(userId, Source.CsvImport, fileName, DateTimeOffset.UtcNow);
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new StagedImport(batch.Id, stagedPath);
    }

    public async Task ProcessCsvImportAsync(Guid batchId, string stagedFilePath,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches.Include(b => b.RowErrors)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            TryDeleteFile(stagedFilePath);
            return;
        }

        try
        {
            var opts = options.Value;
            var totalRows = await CountDataRowsAsync(stagedFilePath, cancellationToken);

            batch.StartProcessing(totalRows, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await progressNotifier.NotifyProgressAsync(ToSummary(batch), cancellationToken);

            var ctx = await BuildDedupContextAsync(batch.UserId, cancellationToken);
            var counts = new RowCounts();

            using (var reader = new StreamReader(stagedFilePath))
            {
                await ProcessCsvAsync(batch.UserId, batch, ctx, reader, Source.CsvImport, resolveJob: false,
                    columnMapping, counts, opts, cancellationToken,
                    onProgress: processed => ReportProgressAsync(batch, processed, cancellationToken));
            }

            batch.Complete(counts.Total, counts.New, counts.Duplicate, counts.Invalid, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await progressNotifier.NotifyCompletedAsync(ToSummary(batch), cancellationToken);
        }
        catch (CsvImportValidationException ex)
        {
            await FailBatchAsync(batch, string.Join(" ", ex.Errors), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailBatchAsync(batch, ex.Message, cancellationToken);
        }
        finally
        {
            TryDeleteFile(stagedFilePath);
        }
    }

    public async Task<StagedImport> StageLinkedInZipImportAsync(Guid userId, Stream zipStream, string fileName,
        long fileLength, CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new CsvImportValidationException([localizer["IMPORT_ONLY_ZIP_SUPPORTED"]]);
        }

        if (fileLength <= 0)
        {
            throw new CsvImportValidationException([localizer["IMPORT_FILE_EMPTY"]]);
        }

        if (fileLength > opts.MaxZipSizeBytes)
        {
            throw new CsvImportValidationException([localizer["IMPORT_ZIP_TOO_LARGE", opts.MaxZipSizeBytes]]);
        }

        var stagedPath = await StageFileAsync(zipStream, ".zip", cancellationToken);

        try
        {
            using (var archive = ZipFile.OpenRead(stagedPath))
            {
                if (archive.Entries.Count > opts.MaxZipEntryCount)
                {
                    throw new CsvImportValidationException([localizer["IMPORT_ZIP_TOO_MANY_ENTRIES", opts.MaxZipEntryCount]]);
                }

                var matchedEntries = archive.Entries.Where(e => JobApplicationsFileRegex().IsMatch(e.Name)).ToList();

                if (matchedEntries.Count == 0)
                {
                    throw new CsvImportValidationException([localizer["IMPORT_ZIP_NO_MATCHING_FILES"]]);
                }

                foreach (var entry in matchedEntries)
                {
                    if (entry.Length > opts.MaxFileSizeBytes)
                    {
                        throw new CsvImportValidationException(
                            [localizer["IMPORT_ZIP_ENTRY_TOO_LARGE", entry.FullName, opts.MaxFileSizeBytes]]);
                    }
                }
            }

            var batch = ImportBatch.Create(userId, Source.LinkedInImport, fileName, DateTimeOffset.UtcNow);
            dbContext.ImportBatches.Add(batch);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new StagedImport(batch.Id, stagedPath);
        }
        catch
        {
            TryDeleteFile(stagedPath);
            throw;
        }
    }

    public async Task ProcessLinkedInZipAsync(Guid batchId, string stagedFilePath, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches.Include(b => b.RowErrors)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            TryDeleteFile(stagedFilePath);
            return;
        }

        try
        {
            var opts = options.Value;
            using var archive = ZipFile.OpenRead(stagedFilePath);
            var matchedEntries = archive.Entries.Where(e => JobApplicationsFileRegex().IsMatch(e.Name)).ToList();

            var totalRows = 0;
            foreach (var entry in matchedEntries)
            {
                totalRows += await CountZipEntryDataRowsAsync(entry, cancellationToken);
            }

            batch.StartProcessing(totalRows, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await progressNotifier.NotifyProgressAsync(ToSummary(batch), cancellationToken);

            var ctx = await BuildDedupContextAsync(batch.UserId, cancellationToken);
            var counts = new RowCounts();

            foreach (var entry in matchedEntries)
            {
                await using var entryStream = new LimitedStream(entry.Open(), opts.MaxFileSizeBytes);
                using var reader = new StreamReader(entryStream);

                try
                {
                    await ProcessCsvAsync(batch.UserId, batch, ctx, reader, Source.LinkedInImport, resolveJob: true,
                        columnMappingOverride: null, counts, opts, cancellationToken,
                        onProgress: processed => ReportProgressAsync(batch, processed, cancellationToken));
                }
                catch (StreamLengthExceededException)
                {
                    throw new CsvImportValidationException([localizer["IMPORT_ZIP_ENTRY_STREAM_EXCEEDED", entry.FullName]]);
                }
            }

            batch.Complete(counts.Total, counts.New, counts.Duplicate, counts.Invalid, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await progressNotifier.NotifyCompletedAsync(ToSummary(batch), cancellationToken);
        }
        catch (CsvImportValidationException ex)
        {
            await FailBatchAsync(batch, string.Join(" ", ex.Errors), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailBatchAsync(batch, ex.Message, cancellationToken);
        }
        finally
        {
            TryDeleteFile(stagedFilePath);
        }
    }

    public async Task<ImportSummaryResponse?> GetByIdAsync(Guid userId, Guid importId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .Include(b => b.RowErrors)
            .FirstOrDefaultAsync(b => b.Id == importId && b.UserId == userId, cancellationToken);

        return batch is null ? null : ToSummary(batch);
    }

    private async Task FailBatchAsync(ImportBatch batch, string errorMessage, CancellationToken cancellationToken)
    {
        batch.Fail(errorMessage, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await progressNotifier.NotifyFailedAsync(ToSummary(batch), cancellationToken);
    }

    private async Task ReportProgressAsync(ImportBatch batch, int processedRows, CancellationToken cancellationToken)
    {
        batch.UpdateProgress(processedRows, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await progressNotifier.NotifyProgressAsync(ToSummary(batch), cancellationToken);
    }

    private static async Task<string> StageFileAsync(Stream sourceStream, string extension, CancellationToken cancellationToken)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ekariyerim-imports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}{extension}");

        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(fileStream, cancellationToken);

        return path;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<int> CountDataRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        return await CountDataRowsAsync(reader, cancellationToken);
    }

    private static async Task<int> CountZipEntryDataRowsAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await CountDataRowsAsync(reader, cancellationToken);
    }

    // Counts newline-delimited rows minus the header line. This is a fast approximation for the
    // progress bar's denominator (a quoted multi-line CSV field would undercount slightly) — the
    // authoritative row count/outcome still comes from CsvHelper during actual processing.
    private static async Task<int> CountDataRowsAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var count = -1;
        while (await reader.ReadLineAsync(cancellationToken) is not null)
        {
            count++;
        }

        return Math.Max(count, 0);
    }

    private async Task ProcessCsvAsync(Guid userId, ImportBatch batch, DedupContext ctx, TextReader reader,
        Source source, bool resolveJob, IReadOnlyDictionary<string, string>? columnMappingOverride,
        RowCounts counts, ImportOptions opts, CancellationToken cancellationToken, Func<int, Task>? onProgress = null)
    {
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
        {
            throw new CsvImportValidationException([localizer["IMPORT_CSV_HEADER_NOT_FOUND"]]);
        }

        var headers = csv.HeaderRecord ?? [];
        var (mapping, mappingErrorCodes) = CsvColumnMapper.Map(headers, columnMappingOverride);
        if (mapping is null)
        {
            throw new CsvImportValidationException(mappingErrorCodes.Select(code => (string)localizer[code]).ToList());
        }

        while (await csv.ReadAsync())
        {
            counts.Total++;

            if (counts.Total > opts.MaxRowCount)
            {
                throw new CsvImportValidationException([localizer["IMPORT_ROW_COUNT_EXCEEDED", opts.MaxRowCount]]);
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

            if (onProgress is not null && counts.Total % ProgressReportInterval == 0)
            {
                await onProgress(counts.Total);
            }
        }

        if (onProgress is not null && counts.Total % ProgressReportInterval != 0)
        {
            await onProgress(counts.Total);
        }
    }

    private async Task<RowOutcome> ProcessRowAsync(Guid userId, ImportBatch batch, DedupContext ctx,
        IReadOnlyDictionary<string, string?> rawRow, ColumnMapping mapping, int rowNumber, string rawRowText,
        Source source, bool resolveJob, CancellationToken cancellationToken)
    {
        var (parsed, error) = ImportRowParser.Parse(rawRow, mapping);
        if (parsed is null)
        {
            batch.AddRowError(rowNumber, rawRowText, localizer[error!.Code, error.Args]);
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
            batch.Id, batch.Source, batch.FileName, batch.Status, batch.ProcessedRows, batch.TotalRows,
            batch.TotalRecords, batch.NewApplications, batch.DuplicateRecords, batch.InvalidRecords,
            batch.CompletedAt, batch.ErrorMessage,
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
