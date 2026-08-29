using AfterApply.Application.Imports.Contracts;

namespace AfterApply.Application.Imports;

public interface IImportService
{
    /// <summary>
    /// Validates the CSV (extension/size), stages it to disk, and creates a Pending
    /// <c>ImportBatch</c>. The returned batch id's row-by-row processing runs later, out of
    /// request scope, via <see cref="ProcessCsvImportAsync"/>.
    /// </summary>
    /// <exception cref="CsvImportValidationException">
    /// File extension or size fails validation before staging.
    /// </exception>
    Task<StagedImport> StageCsvImportAsync(Guid userId, Stream csvStream, string fileName, long fileLength,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the staged CSV import to completion (or failure), updating and pushing progress as
    /// it goes. Invoked out-of-request by a background job — never call this inline with an
    /// HTTP request.
    /// </summary>
    Task ProcessCsvImportAsync(Guid batchId, string stagedFilePath,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the ZIP (extension/size/entry count/has matching files), stages it to disk, and
    /// creates a Pending <c>ImportBatch</c>. Row-by-row processing runs later via
    /// <see cref="ProcessLinkedInZipAsync"/>.
    /// </summary>
    /// <exception cref="CsvImportValidationException">
    /// ZIP size/entry-count limits, per-entry size, or "no matching Job Applications*.csv files
    /// found" fails validation before staging.
    /// </exception>
    Task<StagedImport> StageLinkedInZipImportAsync(Guid userId, Stream zipStream, string fileName, long fileLength,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs the staged ZIP import to completion (or failure), updating and pushing progress as
    /// it goes. Invoked out-of-request by a background job — never call this inline with an
    /// HTTP request.
    /// </summary>
    Task ProcessLinkedInZipAsync(Guid batchId, string stagedFilePath, CancellationToken cancellationToken);

    Task<ImportSummaryResponse?> GetByIdAsync(Guid userId, Guid importId, CancellationToken cancellationToken);
}

public sealed record StagedImport(Guid BatchId, string StagedFilePath);
