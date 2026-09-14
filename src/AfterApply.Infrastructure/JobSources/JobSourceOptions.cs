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

    /// <summary>kariyer.net as a second source next to LinkedIn (2026-09-14). Its robots.txt allows the
    /// listing and the site answers a plain fetch, so the posture is the same honest client with the
    /// same per-source request ceiling and cooldown; off means only LinkedIn is searched and existing
    /// kariyer.net queries are left alone.</summary>
    public bool KariyerNetEnabled { get; init; } = true;

    /// <summary>The fit-scoring model, bound from <c>JobSources:Scoring</c>.</summary>
    public JobFitScoringSettings Scoring { get; init; } = new();
}

/// <summary>
/// The model call behind the fit score, and the ceilings the Pro cost model (DECISIONS.md
/// 2026-09-12) was built on. Every number bounds money: a posting scored is a call paid for, so
/// the per-user, per-day and per-month limits are configuration rather than constants.
/// </summary>
public sealed class JobFitScoringSettings
{
    /// <summary>Name of the named HttpClient the scorer calls Vertex through — the integration
    /// suite blocks outbound HTTP for every client by name (see NoOutboundHttpStartup).</summary>
    public const string HttpClientName = "vertex-job-fit";

    /// <summary>Scoring as a whole. Off means the sweep still fetches and delivers, and nothing
    /// is ever sent to the model; the list shows unscored postings. The GCP project has to be set
    /// as well for a call to happen (an empty project id is refused, not guessed).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The GCP project the model is called in. Empty means unconfigured, and the scorer
    /// skips with a warning rather than guessing — a wrong project id would be a silent bill in
    /// somebody else's account.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>The Vertex region — a privacy decision, not a latency one: pinned to the EU so a
    /// CV's text is processed where the rest of the product's data already lives, and no new
    /// country appears in the privacy policy. Never <c>global</c>.</summary>
    public string Location { get; init; } = "europe-west1";

    /// <summary>The cost model's choice: 2.5 Flash is the cheapest model whose reasoning on a
    /// CV-versus-posting comparison was judged good enough; Flash-Lite is for the CV scan's
    /// writing notes, which is a lighter task.</summary>
    public string Model { get; init; } = "gemini-2.5-flash";

    /// <summary>Characters of the CV the model sees. Fifteen thousand is a long CV in full.</summary>
    public int MaxCvCharacters { get; init; } = 15_000;

    /// <summary>Characters of the posting description the model sees. The requirements are in the
    /// first few thousand; a longer description is mostly the company's boilerplate.</summary>
    public int MaxDescriptionCharacters { get; init; } = 8_000;

    /// <summary>Postings scored per user per week. At the default weekly delivery cap (50) this
    /// scores everything delivered; lower it to score only the best-ranked part of the list.</summary>
    public int MaxPerUserPerWeek { get; init; } = 50;

    /// <summary>Calls per UTC day across every user, counted from the usage ledger. The kill-switch
    /// against a runaway loop rather than a product limit.</summary>
    public int MaxCallsPerDay { get; init; } = 2_000;

    /// <summary>Spend per calendar month (UTC) at the prices below, computed from the ledger's
    /// tokens. Reached, the scorer stops for the rest of the month and the sweep still delivers
    /// unscored postings — "never at a loss" is the rule the whole paid plan rests on.</summary>
    public decimal MonthlyBudgetUsd { get; init; } = 50m;

    /// <summary>List prices used to turn the ledger's tokens into the number the budget is
    /// checked against; gemini-2.5-flash on 2026-09-12. Update when the model or the price changes.</summary>
    public decimal InputUsdPerMillionTokens { get; init; } = 0.30m;

    public decimal OutputUsdPerMillionTokens { get; init; } = 2.50m;

    public int TimeoutSeconds { get; init; } = 45;

    /// <summary>Gemini "thinking" token budget for the call; 0 turns it off. The score is a
    /// reading-and-comparing task that a 2.5 Flash answers fine without it, and with it on the
    /// reasoning eats the output ceiling and the answer comes back empty (2026-09-14 eval).
    /// Null defers to the model's default.</summary>
    public int? ThinkingBudget { get; init; } = 0;
}
