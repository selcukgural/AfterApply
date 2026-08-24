namespace AfterApply.Infrastructure.Imports;

public sealed class ImportOptions
{
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    public int MaxRowCount { get; init; } = 5000;
}
