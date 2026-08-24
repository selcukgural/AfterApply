using System.Globalization;
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

internal sealed class ImportService(AppDbContext dbContext, ICompanyResolver companyResolver, IOptions<ImportOptions> options)
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

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
        {
            throw new CsvImportValidationException(["CSV dosyasında başlık satırı bulunamadı."]);
        }

        var headers = csv.HeaderRecord ?? [];
        var (mapping, mappingErrors) = CsvColumnMapper.Map(headers, columnMapping);
        if (mapping is null)
        {
            throw new CsvImportValidationException(mappingErrors);
        }

        var existingApplications = await dbContext.Applications
            .Where(a => a.UserId == userId)
            .Select(a => new { a.CompanyId, a.JobTitle, a.JobUrl, a.AppliedAt })
            .ToListAsync(cancellationToken);

        var existingUrls = new HashSet<string>(
            existingApplications.Where(a => !string.IsNullOrWhiteSpace(a.JobUrl)).Select(a => a.JobUrl!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var existingCompanyTitleDates = existingApplications
            .Select(a => (a.CompanyId, NormalizedTitle: JobTitleNormalizer.Normalize(a.JobTitle), Date: DateOnly.FromDateTime(a.AppliedAt.UtcDateTime)))
            .ToHashSet();

        var batch = ImportBatch.Create(userId, fileName, DateTimeOffset.UtcNow);
        dbContext.ImportBatches.Add(batch);

        var totalRecords = 0;
        var newApplications = 0;
        var duplicateRecords = 0;
        var invalidRecords = 0;

        while (await csv.ReadAsync())
        {
            totalRecords++;

            if (totalRecords > opts.MaxRowCount)
            {
                throw new CsvImportValidationException([$"Satır sayısı {opts.MaxRowCount} sınırını aşıyor."]);
            }

            var rawRow = headers.ToDictionary(h => h, h => csv.GetField(h));

            var (parsed, error) = ImportRowParser.Parse(rawRow, mapping);
            if (parsed is null)
            {
                invalidRecords++;
                batch.AddRowError(totalRecords, string.Join(", ", headers.Select(h => $"{h}={csv.GetField(h)}")), error!);
                dbContext.ImportRowErrors.Add(batch.RowErrors.Last());
                continue;
            }

            if (parsed.JobUrl is not null && existingUrls.Contains(parsed.JobUrl))
            {
                duplicateRecords++;
                continue;
            }

            var companyId = await companyResolver.ResolveOrCreateAsync(parsed.CompanyName, cancellationToken);
            var normalizedTitle = JobTitleNormalizer.Normalize(parsed.JobTitle);
            var appliedDate = DateOnly.FromDateTime(parsed.AppliedAt.UtcDateTime);

            if (existingCompanyTitleDates.Contains((companyId, normalizedTitle, appliedDate)))
            {
                duplicateRecords++;
                continue;
            }

            var application = DomainApplication.Create(userId, companyId, parsed.JobTitle, parsed.JobUrl, parsed.Location,
                EmploymentType.FullTime, parsed.AppliedAt, Source.CsvImport, notes: null, DateTimeOffset.UtcNow);

            if (parsed.Status != ApplicationStatus.Applied)
            {
                application.ChangeStatus(parsed.Status, parsed.AppliedAt, Source.CsvImport, note: null);
            }

            dbContext.Applications.Add(application);
            newApplications++;

            if (parsed.JobUrl is not null)
            {
                existingUrls.Add(parsed.JobUrl);
            }

            existingCompanyTitleDates.Add((companyId, normalizedTitle, appliedDate));
        }

        batch.Complete(totalRecords, newApplications, duplicateRecords, invalidRecords, DateTimeOffset.UtcNow);
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

    private static ImportSummaryResponse ToSummary(ImportBatch batch)
    {
        return new ImportSummaryResponse(
            batch.Id, batch.FileName, batch.TotalRecords, batch.NewApplications, batch.DuplicateRecords,
            batch.InvalidRecords, batch.CompletedAt,
            batch.RowErrors.Select(e => new ImportRowErrorResponse(e.RowNumber, e.RawRow, e.ErrorMessage)).ToList());
    }
}
