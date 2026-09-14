namespace AfterApply.Api.Middleware;

/// <summary>The one decision the middleware makes, kept pure so it can be unit-tested without a host.</summary>
public static class RequestAuditPolicy
{
    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    /// <summary>
    /// A request carries user input when it is a write verb aimed at a real API endpoint. Reads
    /// (GET/HEAD/OPTIONS) are not input. Anything that did not match an endpoint (a 404) is noise,
    /// and everything outside <c>/api</c> — the health check, SignalR's negotiate handshake — is
    /// plumbing, not a person typing. An endpoint that carries <see cref="SkipRequestAuditMetadata"/>
    /// has opted out on purpose.
    /// </summary>
    public static bool ShouldAudit(HttpContext httpContext)
    {
        if (!AuditedMethods.Contains(httpContext.Request.Method))
        {
            return false;
        }

        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        var endpoint = httpContext.GetEndpoint();
        return endpoint is not null && endpoint.Metadata.GetMetadata<SkipRequestAuditMetadata>() is null;
    }
}
