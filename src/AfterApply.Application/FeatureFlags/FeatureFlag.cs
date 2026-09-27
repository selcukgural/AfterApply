namespace AfterApply.Application.FeatureFlags;

/// <summary>
/// Every product switch an admin can turn at runtime. The member name is what the database
/// stores, so a member is never renamed; a new flag is a new member plus its line in
/// <c>FeatureFlagDefinitions</c>. Each one's deploy default is still the <c>Enabled</c> value
/// of its options section (named in the comment), read when no override exists.
/// </summary>
public enum FeatureFlag
{
    /// <summary><c>Board:Enabled</c> — the applications board.</summary>
    Board,

    /// <summary><c>Blog:Enabled</c> — the blog and the guide.</summary>
    Blog,

    /// <summary><c>CompanyReviews:Enabled</c>.</summary>
    CompanyReviews,

    /// <summary><c>CompanySalaries:Enabled</c>.</summary>
    CompanySalaries,

    /// <summary><c>CandidateExperiences:Enabled</c>.</summary>
    CandidateExperiences,

    /// <summary><c>CompanyIntelligence:Enabled</c> — the company page's response tab (legal read pending).</summary>
    CompanyIntelligence,

    /// <summary><c>ResponseRates:Enabled</c> — the public sector response-rate table.</summary>
    ResponseRates,

    /// <summary><c>SilenceReports:Enabled</c>.</summary>
    SilenceReports,

    /// <summary><c>CvScan:Enabled</c> — the CV scan as a whole.</summary>
    CvScan,

    /// <summary><c>CvScan:LlmEnabled</c> — the CV scan's model-written notes (Vertex AI).</summary>
    CvScanNotes,

    /// <summary><c>EmailForwarding:Enabled</c> — the extension's e-mail signal intake and suggestions.</summary>
    EmailSignals,

    /// <summary><c>EmailAutoApproval:Enabled</c> — applying confident e-mail suggestions without asking.</summary>
    EmailAutoApproval,

    /// <summary><c>Feedback:GitHub:Enabled</c> — mirroring in-app feedback into GitHub issues.</summary>
    FeedbackGitHub,

    /// <summary><c>AtsSources:Enabled</c> — reading a captured ATS posting back from its public API.</summary>
    AtsSources,

    /// <summary><c>JobSources:Enabled</c> — the weekly job matching.</summary>
    JobSources,

    /// <summary><c>PayTr:Enabled</c> — the Pro checkout.</summary>
    Payments,

    /// <summary><c>JobLiveness:Enabled</c> — the daily look at whether saved postings are still up.</summary>
    JobLiveness,

    /// <summary><c>SalaryMarket:Enabled</c> — the public occupation salary pages built from outside surveys.</summary>
    SalaryMarket
}

/// <summary>
/// Whether a flag is on right now. Answered from memory — the overrides are loaded from the
/// database at start-up and reloaded on every change (Redis pub/sub) and on a short poll — so it
/// is cheap enough for an endpoint filter and never waits on I/O.
/// </summary>
public interface IFeatureFlags
{
    bool IsEnabled(FeatureFlag flag);
}

/// <summary>
/// The one way a flag's name is read — from a route or a stored row. By member name only
/// (case-insensitive): <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> alone would
/// also take "3" or "99", turning a typo into some other flag or into one that does not exist.
/// </summary>
public static class FeatureFlagNames
{
    private static readonly Dictionary<string, FeatureFlag> ByName =
        Enum.GetValues<FeatureFlag>().ToDictionary(f => f.ToString(), StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? name, out FeatureFlag flag)
    {
        flag = default;
        return name is not null && ByName.TryGetValue(name, out flag);
    }

    /// <summary>For a name <see cref="TryParse"/> already accepted.</summary>
    public static FeatureFlag ParseKnown(string name) => ByName[name];
}
