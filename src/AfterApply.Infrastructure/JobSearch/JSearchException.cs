namespace AfterApply.Infrastructure.JobSearch;

/// <summary>Why a JSearch call did not produce data. Mapped to the user-facing
/// <c>JOB_SEARCH_*</c> codes by JobSearchService; the client itself only classifies.</summary>
public enum JSearchFailure
{
    /// <summary>No key configured — nothing was sent.</summary>
    NotConfigured,

    /// <summary>RapidAPI gateway 401/403: bad key or no subscription. Never billed.</summary>
    Unauthorized,

    /// <summary>429 from the gateway (per-second limit, or the monthly hard limit).</summary>
    RateLimited,

    /// <summary>The provider's own <c>status: ERROR</c> with a 4xx code — a parameter it rejected.</summary>
    BadRequest,

    /// <summary>5xx, a 404 on a route, or an ERROR envelope with a non-4xx code.</summary>
    Upstream,

    /// <summary>Connection failure or timeout.</summary>
    Transport,

    /// <summary>A body that is not the JSON shape the provider documents.</summary>
    Malformed
}

public sealed class JSearchException(JSearchFailure failure, int? statusCode, string? requestId, string message)
    : Exception(message)
{
    public JSearchFailure Failure { get; } = failure;

    public int? StatusCode { get; } = statusCode;

    public string? RequestId { get; } = requestId;

    /// <summary>HTTP attempts made before giving up; each one the gateway saw was billed.</summary>
    public int Attempts { get; set; } = 1;
}
