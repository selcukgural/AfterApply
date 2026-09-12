using AfterApply.Application.JobSearch;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// JSearch (OpenWeb Ninja, via the RapidAPI marketplace), bound from the <c>JobSearch</c> section.
/// Off unless <see cref="Enabled"/> is true <i>and</i> a key is set, which is why every local run
/// and production-until-the-secret-exists simply 404 the job-search routes and never call out.
///
/// The numbers are sized for the BASIC plan: 200 requests a month, hard-limited, where a search
/// page and a details id each cost one. Everything that protects that budget — the per-user
/// daily ceiling, the global monthly ceiling, the caps on pages and ids per call, the cache
/// lifetimes — is here, and each has a per-user override on JobSearchUserSettings except the
/// monthly ceiling, which is the whole product's.
/// </summary>
public sealed class JobSearchOptions
{
    public const string SectionName = "JobSearch";

    /// <summary>
    /// <b>This flag and the privacy policy move together, in both directions.</b> The published
    /// page (<c>/{locale}/privacy</c>, section <c>#cross-border-transfer</c>) names OpenWeb Ninja
    /// (via RapidAPI, United States) as the recipient of the words a user types into a job or
    /// salary search. That text describes a transfer that only happens while this is true.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>The RapidAPI application key. Secret Manager in production, user-secrets locally.</summary>
    public string? ApiKey { get; init; }

    public string Host { get; init; } = "jsearch.p.rapidapi.com";

    public string BaseUrl { get; init; } = "https://jsearch.p.rapidapi.com/";

    /// <summary>30, not the 15 first tried: the provider's own playground reports ~6 s median
    /// latency, and a live call on 2026-09-12 hit the 15 s limit twice in a row — and the meter
    /// billed both attempts. A longer wait is cheaper than a retry.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Pause before the single retry a transient failure gets. Tests set 0.</summary>
    public int RetryDelayMilliseconds { get; init; } = 2000;

    /// <summary>In-process ceiling on calls per second to the provider; 0 leaves it off. BASIC
    /// has no per-second rule (its limit is 1000 an hour, moot under a 200-a-month quota), so it
    /// is off by default; PRO and above are rate-limited at 5, 10, 20 a second, and this is where
    /// that goes when the plan changes. Per instance, not global — the ledger is the global backstop.</summary>
    public int RequestsPerSecond { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The product's audience is in Türkiye, and JSearch only
    /// returns a country's postings when told the country. There is deliberately no matching
    /// default language — see EffectiveJobSearchSettings.</summary>
    public string DefaultCountry { get; init; } = "tr";

    /// <summary>Server cap on <c>num_pages</c>, well under the provider's 20: each page is a
    /// credit, and a client that wants more pages should walk the cursor one page at a time.</summary>
    public int MaxPagesPerSearch { get; init; } = 3;

    /// <summary>Server cap on ids per details call, under the provider's 20 — each id is a credit.</summary>
    public int MaxJobIdsPerDetails { get; init; } = 5;

    public int PerUserDailyCredits { get; init; } = 10;

    /// <summary>Global ceiling per billing month, counted from the ledger. 20 under BASIC's hard
    /// 200 because two calls that pass the check at the same instant both go through.</summary>
    public int GlobalMonthlyCredits { get; init; } = 180;

    /// <summary>Day of the month (1–28) the RapidAPI quota resets — the subscription's anniversary,
    /// not the first of the month. Left at 1 the ledger and the provider's dashboard drift.</summary>
    public int MonthlyResetDay { get; init; } = 1;

    public int SearchCacheHours { get; init; } = 24;

    public int JobDetailsCacheHours { get; init; } = 24 * 7;

    /// <summary>Both salary endpoints. The provider's figures are monthly aggregates anyway.</summary>
    public int SalaryCacheHours { get; init; } = 24 * 30;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);

    public JobSearchGlobalDefaults ToGlobalDefaults() =>
        new(DefaultCountry, MaxPagesPerSearch, MaxJobIdsPerDetails, PerUserDailyCredits);
}
