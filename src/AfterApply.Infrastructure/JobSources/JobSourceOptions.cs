namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The weekly job-source sweep, bound from the <c>JobSources</c> section. Off by default: while
/// <see cref="Enabled"/> is false the sweep is a no-op and every <c>/api/job-sources/*</c> route
/// 404s, so nothing here reaches LinkedIn until the flag is deliberately turned on.
///
/// The numbers are the "low volume" the decision to fetch from LinkedIn at all was conditioned on
/// (DECISIONS.md 2026-09-12): LinkedIn's robots.txt forbids automated access outright, and the
/// only defensible posture is to look like what we are — one honest client, a couple of hundred
/// requests a day at most, backing off the moment the site says no.
/// </summary>
public sealed class JobSourceOptions
{
    public const string SectionName = "JobSources";

    /// <summary><b>This flag and the privacy policy move together.</b> The published page names
    /// LinkedIn as the recipient of the job titles and location a user saves; that transfer only
    /// happens while this is true.</summary>
    public bool Enabled { get; init; }

    /// <summary>Hard ceiling on requests to the source per UTC day, across every instance (the
    /// ledger is in the database). Search pages and posting details count alike.</summary>
    public int MaxRequestsPerDay { get; init; } = 200;

    /// <summary>Pages of ten per query. Past the exact-match count LinkedIn pads the list with
    /// loosely related postings, so more pages buy noise, not coverage.</summary>
    public int MaxPagesPerQuery { get; init; } = 5;

    /// <summary>Pause between consecutive requests, plus up to the same again of jitter. Tests set 0.</summary>
    public int MinDelayMs { get; init; } = 1500;

    /// <summary>How long the sweep stays stopped after the source answers 429/403 or shows a login
    /// wall. Persisted through the ledger, so a new instance honours a block an old one hit.</summary>
    public int CircuitCooldownHours { get; init; } = 24;

    /// <summary>A query is fetched at most once per this many days, however many users share it.</summary>
    public int QueryShareDays { get; init; } = 7;

    /// <summary>Postings no query has surfaced for this long, and that were never delivered, are deleted.</summary>
    public int RetentionDays { get; init; } = 60;

    /// <summary>The product cap the cost model was built on; per-user override on UserJobSourceSettings.</summary>
    public int DefaultWeeklyPostingsPerUser { get; init; } = 50;

    /// <summary>A user with no sign-in (refresh token issued) in this many days is skipped — no
    /// point spending the budget on someone who will not read the result.</summary>
    public int InactiveAfterDays { get; init; } = 30;

    /// <summary>Monday 04:00 UTC, as the product plan says. Cloud Run at min-instances=0 can miss
    /// it; the sweep is idempotent within a week, so a late run does no harm.</summary>
    public string Cron { get; init; } = "0 4 * * 1";

    /// <summary>Distinct from the link-preview client's, so the two kinds of traffic can be told
    /// apart on the other side.</summary>
    public string UserAgent { get; init; } = "EKariyerimJobSource/1.0 (+https://ekariyerim.com)";

    /// <summary>Base of the exponential backoff between retries of a transient failure. Tests set a few ms.</summary>
    public int RetryBaseDelayMs { get; init; } = 1000;

    /// <summary>Per-attempt timeout inside the resilience pipeline.</summary>
    public int AttemptTimeoutSeconds { get; init; } = 10;

    /// <summary>Whole-call timeout across retries.</summary>
    public int TotalTimeoutSeconds { get; init; } = 30;
}
