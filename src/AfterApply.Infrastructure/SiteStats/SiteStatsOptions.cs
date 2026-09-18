namespace AfterApply.Infrastructure.SiteStats;

public sealed class SiteStatsOptions
{
    public const string SectionName = "SiteStats";

    /// <summary>A counter is shown from this many upwards. Twenty-five: enough that "N people
    /// did this" is a fact about the site rather than about its first week, and the same order as
    /// the benchmark's own sector floor (30) without waiting for it.</summary>
    public int MinimumCount { get; init; } = 25;

    /// <summary>How long the three counts are cached, in seconds. An hour: the landing page is
    /// served statically and re-fetches on the same cadence, and a counter that lags an hour is
    /// still a counter.</summary>
    public int CacheSeconds { get; init; } = 3600;
}
