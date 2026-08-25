using AfterApply.Application.Imports.Contracts;

namespace AfterApply.Application.Imports;

public interface IImportService
{
    /// <exception cref="CsvImportValidationException">
    /// File extension/size, row count, or column mapping fails validation before any row is processed.
    /// </exception>
    Task<ImportSummaryResponse> ImportCsvAsync(Guid userId, Stream csvStream, string fileName, long fileLength,
        IReadOnlyDictionary<string, string>? columnMapping, CancellationToken cancellationToken);

    /// <exception cref="CsvImportValidationException">
    /// ZIP size/entry-count limits, per-entry size, or "no matching Job Applications*.csv files found"
    /// fails validation before any row is processed.
    /// </exception>
    Task<ImportSummaryResponse> ImportLinkedInZipAsync(Guid userId, Stream zipStream, string fileName,
        long fileLength, CancellationToken cancellationToken);

    Task<ImportSummaryResponse?> GetByIdAsync(Guid userId, Guid importId, CancellationToken cancellationToken);
}
