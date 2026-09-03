using System.Globalization;
using System.Threading.RateLimiting;
using AfterApply.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AfterApply.Api;

public static class RateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // ASP.NET Core's default rejection status is 503 — 429 is the conventional
            // status for rate limiting and what clients are expected to handle.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, _) =>
            {
                // Without this a client has nothing to back off against and the sensible thing for
                // it to do — retry immediately — is the worst thing for us.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning("Rate limit exceeded for {RemoteIp} on {Path}",
                    context.HttpContext.Connection.RemoteIpAddress, context.HttpContext.Request.Path);
                return ValueTask.CompletedTask;
            };

            // Backstop over every endpoint, including the ones with no named policy of their own.
            // Before this existed, only auth/upload/extension-signal were bounded at all, so an
            // authenticated caller could hammer anything else — /api/users/me/export (loads the
            // account's entire history), /api/companies/search (a trigram scan per keystroke) —
            // as fast as the network allowed. Sized to be invisible to real use: the busiest screen
            // fires well under a dozen requests, and the polling badges are on multi-second timers.
            //
            // Partitioned by user where there is one and by IP otherwise, the same split the named
            // policies below use. This runs after UseAuthentication (see Program.cs's pipeline
            // order), so the sub claim is already available here.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            // IP-based: auth endpoints are called before the caller is authenticated.
            options.AddPolicy(DependencyInjection.AuthRateLimitPolicy, httpContext =>
            {
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            // User-based: upload endpoints already require auth, so this is more precise
            // than IP-based (avoids penalizing legitimate users sharing a NAT'd IP).
            options.AddPolicy(DependencyInjection.UploadRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));

            // User-based, same idiom as UploadRateLimitPolicy. A backstop against a buggy/looping
            // Gmail content script, not the primary control: the extension's own client-side dedup
            // of already-submitted thread ids is what normally keeps volume low, since a user only
            // opens so many emails per session.
            options.AddPolicy(DependencyInjection.ExtensionSignalRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));

            // Tighter than the global limit because this endpoint is the only one that makes an
            // outbound request to a third party (JobLinkPreviewService fetches the pasted URL from
            // linkedin.com/kariyer.net). Left at the global 300/min it would let one account point
            // a few hundred requests a minute at someone else's servers over our IP — the kind of
            // amplification that gets an egress address blocked. A human pastes one link at a time.
            options.AddPolicy(DependencyInjection.LinkPreviewRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));
        });

        return services;
    }

    /// <summary>The authenticated user where there is one, the caller's IP otherwise. The IP is the
    /// real client's only because UseForwardedHeaders runs first (Program.cs) — without that every
    /// anonymous caller behind Cloud Run's frontend shares a single partition.</summary>
    private static string PartitionKey(HttpContext httpContext) =>
        httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
