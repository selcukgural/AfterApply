namespace AfterApply.Infrastructure.CandidateExperiences;

public sealed class CandidateExperienceOptions
{
    public const string SectionName = "CandidateExperiences";

    /// <summary>
    /// Off → every candidate-experience endpoint answers 404, the public company page reports
    /// zero entries and the web app hides the menu links and the tab — the
    /// <c>CompanySalaries:Enabled</c> shape, so the feature can ship dark and be switched on
    /// without a deploy.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How many experiences one account may hold across all companies (one per company).
    /// Deleting one frees the slot.</summary>
    public int MaxEntriesPerUser { get; init; } = 10;

    /// <summary>Below this many entries a company shows the count and the individual cards but no
    /// score, category average, "most picked" list or process statistic — one knob for every
    /// aggregate. Raising it hides more and is always safe.</summary>
    public int MinimumEntriesForStats { get; init; } = 3;

    /// <summary>The <c>m</c> of the Bayesian score (see <c>CompanyReviewScoring</c>).</summary>
    public int PriorWeight { get; init; } = 5;

    public int PageSize { get; init; } = 10;
}
