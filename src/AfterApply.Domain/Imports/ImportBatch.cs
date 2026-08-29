using AfterApply.Domain.Common;

namespace AfterApply.Domain.Imports;

public sealed class ImportBatch : AuditableEntity
{
    private readonly List<ImportRowError> _rowErrors = [];

    public Guid UserId { get; private set; }

    public Source Source { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public ImportBatchStatus Status { get; private set; } = ImportBatchStatus.Pending;

    public int ProcessedRows { get; private set; }

    public int? TotalRows { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int TotalRecords { get; private set; }

    public int NewApplications { get; private set; }

    public int DuplicateRecords { get; private set; }

    public int InvalidRecords { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<ImportRowError> RowErrors => _rowErrors;

    private ImportBatch()
    {
    }

    public static ImportBatch Create(Guid userId, Source source, string fileName, DateTimeOffset now)
    {
        return new ImportBatch
        {
            UserId = userId,
            Source = source,
            FileName = fileName,
            Status = ImportBatchStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AddRowError(int rowNumber, string rawRow, string errorMessage)
    {
        _rowErrors.Add(ImportRowError.Create(Id, rowNumber, rawRow, errorMessage));
    }

    public void StartProcessing(int? totalRows, DateTimeOffset now)
    {
        Status = ImportBatchStatus.Processing;
        TotalRows = totalRows;
        Touch(now);
    }

    public void UpdateProgress(int processedRows, DateTimeOffset now)
    {
        ProcessedRows = processedRows;
        Touch(now);
    }

    public void Complete(int totalRecords, int newApplications, int duplicateRecords, int invalidRecords, DateTimeOffset completedAt)
    {
        TotalRecords = totalRecords;
        NewApplications = newApplications;
        DuplicateRecords = duplicateRecords;
        InvalidRecords = invalidRecords;
        ProcessedRows = totalRecords;
        Status = ImportBatchStatus.Completed;
        CompletedAt = completedAt;
        Touch(completedAt);
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        Status = ImportBatchStatus.Failed;
        ErrorMessage = errorMessage;
        Touch(now);
    }
}
