using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSources;

/// <summary>
/// The ledger: one row per HTTP request the sweep made to a source. The daily request budget and
/// the 24-hour stop after a block are both sums over this table, which makes them shared across
/// Cloud Run instances rather than per process. Never the URL and never the keywords — an
/// outcome and a duration are all a budget needs.
/// </summary>
public sealed class JobSourceFetch : Entity
{
    public Source Source { get; private set; }

    public JobSourceFetchKind Kind { get; private set; }

    public DateTimeOffset At { get; private set; }

    public int? StatusCode { get; private set; }

    public JobSourceFetchOutcome Outcome { get; private set; }

    public int DurationMs { get; private set; }

    private JobSourceFetch()
    {
    }

    public static JobSourceFetch Create(Source source, JobSourceFetchKind kind, int? statusCode,
        JobSourceFetchOutcome outcome, int durationMs, DateTimeOffset now) =>
        new() { Source = source, Kind = kind, StatusCode = statusCode, Outcome = outcome, DurationMs = durationMs, At = now };
}

public enum JobSourceFetchKind
{
    Search,
    Detail
}

public enum JobSourceFetchOutcome
{
    Ok,
    /// <summary>The source answered 429: slow down. The sweep stops for the cooldown.</summary>
    RateLimited,
    /// <summary>The source answered 403/999 or redirected to a login/auth wall: it does not want
    /// this traffic. The sweep stops for the cooldown.</summary>
    Blocked,
    /// <summary>Transport failure or unexpected status; counted against the budget, does not stop the sweep.</summary>
    Error
}
