namespace AfterApply.Api.Middleware;

/// <summary>
/// Endpoint marker that keeps <see cref="RequestAuditMiddleware"/> from writing a row. Attached
/// with <c>WithoutRequestAudit()</c>, and only there: an opt-out is a privacy statement, so every
/// use carries a comment saying why, and the integration suite pins the exact set of routes that
/// carry it (RequestAuditTests) so a new one cannot slip in unnoticed.
/// </summary>
public sealed class SkipRequestAuditMetadata
{
    public static readonly SkipRequestAuditMetadata Instance = new();

    private SkipRequestAuditMetadata()
    {
    }
}
