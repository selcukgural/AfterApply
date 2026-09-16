namespace AfterApply.Infrastructure.Occupations;

public sealed class OccupationSearchOptions
{
    public const string SectionName = "Occupations";

    /// <summary>Autocomplete queries shorter than this return an empty list (the web checks the
    /// same length before firing a request).</summary>
    public int MinQueryLength { get; init; } = 2;

    public int MaxResults { get; init; } = 10;
}
