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
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning("Rate limit exceeded for {RemoteIp} on {Path}",
                    context.HttpContext.Connection.RemoteIpAddress, context.HttpContext.Request.Path);
                return ValueTask.CompletedTask;
            };

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
            {
                var partitionKey = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                });
            });

            // Per-inbound-token, not per-IP — the Cloudflare Worker's egress IP is shared
            // infrastructure, not per-user. Guards against a flood (misbehaving sender, retry storm)
            // turning into unbounded LLM-classification cost; each request past the cap is dropped,
            // not queued, so a burst degrades gracefully rather than backing up.
            options.AddPolicy(DependencyInjection.InboundEmailRateLimitPolicy, httpContext =>
            {
                var partitionKey = httpContext.Request.Headers["X-Inbound-Token"].FirstOrDefault() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                });
            });

            // User-based, same idiom as UploadRateLimitPolicy — this is authenticated (unlike
            // /inbound). A backstop against a buggy/looping Gmail content script, not the primary
            // control: the extension's own client-side dedup of already-submitted thread ids is what
            // normally keeps volume low, since a user only opens so many emails per session.
            options.AddPolicy(DependencyInjection.ExtensionSignalRateLimitPolicy, httpContext =>
            {
                var partitionKey = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}
