using AfterApply.Domain.Common;

namespace AfterApply.Domain.Imports;

public sealed class ImportRowError : Entity
{
    public Guid ImportBatchId { get; private set; }

    public int RowNumber { get; private set; }

    public string RawRow { get; private set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    private ImportRowError()
    {
    }

    internal static ImportRowError Create(Guid importBatchId, int rowNumber, string rawRow, string errorMessage)
    {
        return new ImportRowError
        {
            ImportBatchId = importBatchId,
            RowNumber = rowNumber,
            RawRow = rawRow,
            ErrorMessage = errorMessage
        };
    }
}
