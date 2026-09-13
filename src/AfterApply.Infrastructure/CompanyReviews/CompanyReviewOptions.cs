namespace AfterApply.Infrastructure.CompanyReviews;

public sealed class CompanyReviewOptions
{
    public const string SectionName = "CompanyReviews";

    /// <summary>
    /// Off → every review endpoint answers 404 and the web app hides the pages, the same shape as
    /// <c>CompanyIntelligence:Enabled</c>. Exists so the feature can be deployed dark: publishing
    /// user-written text under a named company is the legal question that keeps K1 parked
    /// (DEVELOPMENT_PLAN.md, Sıra 5), and a flag lets that answer arrive after the code does.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How many reviews one account may hold across all companies, in any status. An
    /// admin can override it per account (<c>Users.ReviewQuotaOverride</c>).</summary>
    public int MaxReviewsPerUser { get; init; } = 10;

    /// <summary>Below this many approved reviews a company shows no score, only the count — the
    /// same asymmetry as the benchmark threshold: raising it hides more and is always safe.</summary>
    public int MinimumReviewsForScore { get; init; } = 3;

    /// <summary>The <c>m</c> in the Bayesian average (see CompanyReviewScoring): how many
    /// "average" reviews a company is assumed to have before its own are counted.</summary>
    public int PriorWeight { get; init; } = 5;

    public int PublicPageSize { get; init; } = 10;

    public int AdminPageSize { get; init; } = 25;
}
