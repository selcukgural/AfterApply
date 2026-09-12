using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSources;

/// <summary>
/// One normalised search — (source, keywords, location, window, remote) — shared by every user
/// whose profile resolves to it. The sweep runs each query at most once per <c>ShareDays</c>, so
/// two users asking for ".NET Developer" in Istanbul cost the source one fetch, not two. The
/// <see cref="KeyHash"/> is the identity; the readable columns are what it was built from.
/// </summary>
public sealed class JobSourceQuery : Entity
{
    public const int MaxKeywordsLength = 100;
    public const int MaxLocationLength = 100;

    public Source Source { get; private set; }

    public string Keywords { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public JobSourceTimeWindow TimeWindow { get; private set; }

    public bool RemoteOnly { get; private set; }

    /// <summary>SHA-256 (hex) of the normalised tuple; unique.</summary>
    public string KeyHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public int? LastResultCount { get; private set; }

    /// <summary>A short outcome code (e.g. "Ok", "RateLimited"), never text from the source.</summary>
    public string? LastOutcome { get; private set; }

    private JobSourceQuery()
    {
    }

    public static JobSourceQuery Create(Source source, string keywords, string location, JobSourceTimeWindow timeWindow,
        bool remoteOnly, string keyHash, DateTimeOffset now)
    {
        return new JobSourceQuery
        {
            Source = source,
            Keywords = keywords,
            Location = location,
            TimeWindow = timeWindow,
            RemoteOnly = remoteOnly,
            KeyHash = keyHash,
            CreatedAt = now
        };
    }

    public bool RanWithin(TimeSpan window, DateTimeOffset now) => LastRunAt is { } last && now - last < window;

    public void RecordRun(int resultCount, string outcome, DateTimeOffset now)
    {
        LastRunAt = now;
        LastResultCount = resultCount;
        LastOutcome = outcome;
    }
}
