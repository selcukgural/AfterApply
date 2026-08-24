using AfterApply.Domain.Common;

namespace AfterApply.Domain.Imports;

public sealed class ImportBatch : AuditableEntity
{
    private readonly List<ImportRowError> _rowErrors = [];

    public Guid UserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public int TotalRecords { get; private set; }

    public int NewApplications { get; private set; }

    public int DuplicateRecords { get; private set; }

    public int InvalidRecords { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public IReadOnlyCollection<ImportRowError> RowErrors => _rowErrors;

    private ImportBatch()
    {
    }

    public static ImportBatch Create(Guid userId, string fileName, DateTimeOffset now)
    {
        return new ImportBatch
        {
            UserId = userId,
            FileName = fileName,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = now
        };
    }

    public void AddRowError(int rowNumber, string rawRow, string errorMessage)
    {
        _rowErrors.Add(ImportRowError.Create(Id, rowNumber, rawRow, errorMessage));
    }

    public void Complete(int totalRecords, int newApplications, int duplicateRecords, int invalidRecords, DateTimeOffset completedAt)
    {
        TotalRecords = totalRecords;
        NewApplications = newApplications;
        DuplicateRecords = duplicateRecords;
        InvalidRecords = invalidRecords;
        Touch(completedAt);
    }
}
