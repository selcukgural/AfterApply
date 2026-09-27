namespace AfterApply.Infrastructure.JobLiveness;

/// <summary>
/// The posting liveness check, bound from the <c>JobLiveness</c> section. Off unless the flag is
/// on (runtime flag <c>JobLiveness</c>; this <see cref="Enabled"/> is its deploy default).
/// </summary>
/// <remarks>
/// The same posture as the job-source sweep (DECISIONS.md 2026-09-12): one honest client, a few
/// hundred requests a day at most, backing off the moment a site says no. What goes out is the
/// posting's public address built from its id — nothing about the person who saved it.
/// </remarks>
public sealed class JobLivenessOptions
{
    public const string SectionName = "JobLiveness";

    /// <summary><b>This flag and the privacy policy move together</b>: the privacy page says saved
    /// postings are re-opened to see whether they are still up.</summary>
    public bool Enabled { get; init; }

    /// <summary>Daily, 03:00 UTC — 06:00 in Istanbul, before anyone opens the dashboard.</summary>
    public string Cron { get; init; } = "0 3 * * *";

    /// <summary>A posting is looked at no more often than this.</summary>
    public int CheckIntervalDays { get; init; } = 3;

    /// <summary>How long a "gone" (bare 404, redirect to the general listing) has to last between
    /// two looks before it closes the posting.</summary>
    public int ConfirmAfterHours { get; init; } = 36;

    /// <summary>Hard ceiling on postings looked at per run, across every site.</summary>
    public int MaxChecksPerRun { get; init; } = 150;

    /// <summary>Applications older than this are not worth a request: the answer no longer changes
    /// anything anyone sees.</summary>
    public int OpenWindowDays { get; init; } = 90;

    /// <summary>Pause between consecutive requests, plus up to the same again of jitter. Tests set 0.</summary>
    public int MinDelayMs { get; init; } = 2000;

    /// <summary>How much of a page is read. kariyer.net's closing date sits around character
    /// 230 000 of a ~300 000-character page; LinkedIn's marker comes much earlier.</summary>
    public int MaxBodyChars { get; init; } = 450_000;

    /// <summary>Its own, so this traffic can be told apart from the link preview's and the sweep's.</summary>
    public string UserAgent { get; init; } = "EKariyerimJobCheck/1.0 (+https://ekariyerim.com)";
}
