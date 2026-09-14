using System.IdentityModel.Tokens.Jwt;
using AfterApply.Api.Extensions;
using AfterApply.Domain.Auditing;
using AfterApply.Infrastructure.Persistence;

namespace AfterApply.Api.Middleware;

/// <summary>
/// Records who asked what from where for every request that carries user input — see
/// <see cref="RequestAudit"/> for why the table exists and <see cref="RequestAuditPolicy"/> for
/// what counts. Sits after authentication (so the <c>sub</c> claim is there for JWT and PAT
/// callers alike) and after the rate limiter (a 429 never reached a handler, so there was no
/// input), which also means a 401 short-circuited by authorization is not recorded.
///
/// The row is written after the handler ran, with the status it produced — a rejected review or
/// a failed sign-in is exactly the kind of thing a later abuse report asks about — and it never
/// changes the outcome: a failed write is logged (without the IP) and the response stands.
/// </summary>
public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        await next(httpContext);

        if (!RequestAuditPolicy.ShouldAudit(httpContext))
        {
            return;
        }

        // Captured before anything else runs on this context — by the time the write below
        // happens the request's own scope may already be tearing down.
        var userId = ResolveUserId(httpContext);
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value ?? "/";
        var statusCode = httpContext.Response.StatusCode;
        var ipAddress = httpContext.GetClientIpAddress();

        try
        {
            // A fresh scope, not the request's DbContext: that one may be tracking half-applied
            // state from a handler that threw, and its lifetime is not ours to extend. No
            // cancellation token on purpose — the client hanging up is not a reason to lose the row.
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.RequestAudits.Add(RequestAudit.Create(userId, method, path, statusCode, ipAddress, DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Request audit row could not be written for {Method} {Path}", method, path);
        }
    }

    private static Guid? ResolveUserId(HttpContext httpContext) =>
        Guid.TryParse(httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
