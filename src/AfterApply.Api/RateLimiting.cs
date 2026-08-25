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
        });

        return services;
    }
}
