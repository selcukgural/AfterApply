namespace AfterApply.Infrastructure.CompanySalaries;

public sealed class CompanySalaryOptions
{
    public const string SectionName = "CompanySalaries";

    /// <summary>
    /// Off → every salary endpoint answers 404, the public company page reports zero entries and
    /// the web app hides the menu links and tabs — the <c>CompanyReviews:Enabled</c> shape, so the
    /// feature can ship dark and be switched on without a deploy.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How many salary entries one account may hold across all companies. Deleting one
    /// frees the slot.</summary>
    public int MaxEntriesPerUser { get; init; } = 10;

    /// <summary>Below this many entries in one currency a company shows no median or range for
    /// it, only the rows — the same asymmetry as the review score threshold: raising it hides
    /// more and is always safe.</summary>
    public int MinimumEntriesForStats { get; init; } = 3;

    public int PageSize { get; init; } = 20;
}
