using Microsoft.Extensions.Configuration;

namespace AfterApply.Infrastructure.Caching;

/// <summary>
/// The one place the Redis connection string is read. Everything that talks to Redis — the cache's
/// L2, its backplane, the health check, and later the rate limiter, SignalR and the distributed
/// lock — shares the single <c>IConnectionMultiplexer</c> built from it, so Memorystore sees one
/// client per Cloud Run instance, not one per feature.
/// </summary>
public static class RedisConnectionString
{
    /// <summary>
    /// What OpenAPI document generation (see <c>DependencyInjection.IsOpenApiDocumentGeneration</c>)
    /// gets instead of a real string: nothing dials it — the multiplexer is created lazily and that
    /// build step never serves traffic — but the fail-fast check below must not block
    /// <c>dotnet build</c>. <c>abortConnect=false</c> for the same reason it is in production's
    /// secret: with the default (<c>true</c>) a Redis that is unreachable at the moment the
    /// multiplexer is first created throws, and a Cloud Run container would never come up.
    /// </summary>
    private const string PlaceholderForOpenApiGeneration = "localhost:6379,abortConnect=false";

    public static string Resolve(IConfiguration configuration, bool isOpenApiDocumentGeneration)
    {
        return configuration.GetConnectionString("Redis")
            ?? (isOpenApiDocumentGeneration ? PlaceholderForOpenApiGeneration : null)
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Redis is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Redis \"localhost:6382,abortConnect=false\" --project src/AfterApply.Api' " +
                "(see README \"Quick start\"), or set ConnectionStrings__Redis when running via docker-compose.");
    }
}
