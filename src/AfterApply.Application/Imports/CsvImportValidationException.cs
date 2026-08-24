namespace AfterApply.Application.Imports;

public sealed class CsvImportValidationException(IReadOnlyList<string> errors) : Exception("CSV import validation failed.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
