using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSearch;

/// <summary>
/// The job-search ledger: one row per call a user made, whether it went upstream or was answered
/// from our own tables. <see cref="Credits"/> is what the call cost against the RapidAPI quota
/// (pages for a search, ids for a details batch, one for a salary lookup, zero for a cache hit),
/// and the per-user daily and global monthly ceilings are sums over this column — the only counter
/// that survives a Cloud Run instance going away.
///
/// A failed upstream call still charges: we cannot see whether RapidAPI billed it, so the ledger
/// assumes it did. The exception is a 401/403 — a rejected key is never billed.
///
/// Counts only: the search text is not here. What was asked lives, without a user, on the cache
/// rows; this table is the one that cascades from the account.
/// </summary>
public sealed class JobSearchUsage : Entity
{
    public Guid UserId { get; private set; }

    public JobSearchOperation Operation { get; private set; }

    public int Credits { get; private set; }

    public bool CacheHit { get; private set; }

    public bool Succeeded { get; private set; }

    public int? StatusCode { get; private set; }

    public string? UpstreamRequestId { get; private set; }

    /// <summary>RapidAPI's <c>x-ratelimit-requests-remaining</c> as observed on this call. The
    /// latest non-null value is the provider's own word on how much of the month is left.</summary>
    public int? UpstreamRequestsRemaining { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    private JobSearchUsage()
    {
    }

    public static JobSearchUsage Create(Guid userId, JobSearchOperation operation, int credits, bool cacheHit,
        bool succeeded, int? statusCode, string? upstreamRequestId, int? upstreamRequestsRemaining,
        DateTimeOffset requestedAt) =>
        new()
        {
            UserId = userId,
            Operation = operation,
            Credits = credits,
            CacheHit = cacheHit,
            Succeeded = succeeded,
            StatusCode = statusCode,
            UpstreamRequestId = upstreamRequestId,
            UpstreamRequestsRemaining = upstreamRequestsRemaining,
            RequestedAt = requestedAt
        };
}
