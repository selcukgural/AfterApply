using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSearch;

/// <summary>
/// One cached JSearch answer for a search or a salary lookup, keyed by a hash of the normalised
/// request. Shared across users and, like <see cref="JobSearchJob"/>, carries <b>no UserId</b> —
/// <see cref="Parameters"/> is the normalised request echo (the query text, the filters), never
/// who asked. Job details are not cached here: they are per-posting rows on
/// <see cref="JobSearchJob"/>, so a batch request can be served id by id.
///
/// The payload is the product's own response DTO as JSON, not the provider's shape, so a hit is
/// one deserialisation and no mapping; <see cref="SchemaVersion"/> guards a row written by a
/// build whose DTO looked different.
/// </summary>
public sealed class JobSearchCacheEntry : Entity
{
    public JobSearchOperation Operation { get; private set; }

    /// <summary>SHA-256 hex of the canonical request string — see JobSearchCacheKey.</summary>
    public string KeyHash { get; private set; } = string.Empty;

    public string? Parameters { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public int SchemaVersion { get; private set; }

    public string? UpstreamRequestId { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    private JobSearchCacheEntry()
    {
    }

    public static JobSearchCacheEntry Create(JobSearchOperation operation, string keyHash, string? parameters,
        string payload, int schemaVersion, string? upstreamRequestId, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var entry = new JobSearchCacheEntry { Operation = operation, KeyHash = keyHash, Parameters = parameters };
        entry.Refresh(payload, schemaVersion, upstreamRequestId, now, expiresAt);
        return entry;
    }

    public void Refresh(string payload, int schemaVersion, string? upstreamRequestId, DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        Payload = payload;
        SchemaVersion = schemaVersion;
        UpstreamRequestId = upstreamRequestId;
        FetchedAt = now;
        ExpiresAt = expiresAt;
    }

    public bool IsFresh(DateTimeOffset now, int schemaVersion) => SchemaVersion == schemaVersion && ExpiresAt > now;
}
