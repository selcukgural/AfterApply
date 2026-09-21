namespace AfterApply.Infrastructure.ResponseRates;

/// <summary>
/// The public, company-less response-rate table (`/api/response-rates/sectors`). Its thresholds
/// are lower than a company page's because no one is named: a sector row says "software
/// companies answered 44% of applications", not "this company did".
/// </summary>
public sealed class ResponseRateOptions
{
    /// <summary>
    /// The page ships on: nothing on it names a company, and the thresholds below keep it from
    /// naming a person. Kept as a flag all the same, like every other public surface, so it can
    /// go dark from config without a deploy.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A sector needs this many different people before it is a row. With one or two contributors
    /// "the sector's response rate" is their response rate, and anyone who knows which sector
    /// they applied in can read it back to them.
    /// </summary>
    public int MinimumContributors { get; init; } = 5;

    /// <summary>The floor under a row's application count — the benchmark uses thirty answers
    /// for a sector median, and this is the same claim at the same size.</summary>
    public int MinimumApplications { get; init; } = 30;

    /// <summary>
    /// The largest share (0–1) of a sector's applications one person may account for. Looser
    /// than a company page's third because a sector with five contributors and one prolific
    /// applicant is still five people's data; tighter than "anything" because at half, the row
    /// is one person's job search plus noise.
    /// </summary>
    public double MaxContributorShare { get; init; } = 0.5;

    /// <summary>See <c>CompanyIntelligenceOptions.MaturityDays</c>; the same rule, the same
    /// default, kept as its own setting.</summary>
    public int MaturityDays { get; init; } = 30;

    /// <summary>
    /// The table scans every application in the window and is read by anyone; an hour is the
    /// balance between a page that reflects today and a database that answers a crawler each
    /// time. Both the in-process and the Redis layer hold it this long.
    /// </summary>
    public int CacheSeconds { get; init; } = 3600;
}
