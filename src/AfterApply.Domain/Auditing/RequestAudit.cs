using AfterApply.Domain.Common;

namespace AfterApply.Domain.Auditing;

/// <summary>
/// One row per request that carried user input: who (if signed in), from which IP, what was asked
/// and how it ended. Written by the API's request-audit middleware for every POST/PUT/PATCH/DELETE
/// under <c>/api</c>, so a new endpoint is covered without anyone remembering to add a line.
///
/// This exists for one reason — an abuse report or a legal request months later asking "which
/// connection wrote this review / logged into this account". It is never read by a product feature,
/// never returned by any endpoint (the account export included — see DECISIONS.md 2026-09-14) and
/// never logged. Only the IP is kept from the connection: no user agent, no body, no query string.
///
/// Rows with a <see cref="UserId"/> go with the account (cascade). Rows without one — an anonymous
/// CV scan, a benchmark answer, a failed sign-in — belong to nobody and are purged by
/// <c>RequestAuditRetentionService</c> after the configured window.
/// </summary>
public sealed class RequestAudit : Entity
{
    public const int MaxMethodLength = 8;
    public const int MaxPathLength = 512;
    /// <summary>An IPv6 address with an embedded IPv4 tail is 45 characters at most.</summary>
    public const int MaxIpAddressLength = 45;

    public Guid? UserId { get; private set; }

    public string Method { get; private set; } = string.Empty;

    /// <summary>The request path only — a query string could carry a token or an e-mail.</summary>
    public string Path { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset At { get; private set; }

    private RequestAudit()
    {
    }

    public static RequestAudit Create(Guid? userId, string method, string path, int statusCode, string? ipAddress, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method is required.", nameof(method));
        }

        return new RequestAudit
        {
            UserId = userId,
            Method = Truncate(method.Trim().ToUpperInvariant(), MaxMethodLength)!,
            Path = Truncate(NullIfBlank(path), MaxPathLength) ?? "/",
            StatusCode = statusCode,
            IpAddress = Truncate(NullIfBlank(ipAddress), MaxIpAddressLength),
            At = at
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
