using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Fetches and caches LinkedIn's OpenID Connect signing keys
/// (https://www.linkedin.com/oauth/openid/jwks), used by <see cref="LinkedInIdTokenReader"/> to
/// verify the RS256 signature on a "Sign in with LinkedIn" ID token. Cached for 24 hours: a token
/// signed with a `kid` this cache doesn't have (LinkedIn rotated its keys since the last fetch) is
/// the one failure mode a stale cache can't recover from by itself — <see cref="LinkedInAuthClient"/>
/// calls <see cref="RefreshAsync"/> once and retries in that case, so a key rotation never needs a
/// redeploy to recover from.
/// </summary>
public sealed class LinkedInJwksProvider(HttpClient httpClient, TimeProvider? timeProvider = null)
{
    private const string JwksEndpoint = "https://www.linkedin.com/oauth/openid/jwks";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private JsonWebKeySet? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<JsonWebKeySet> GetAsync(CancellationToken cancellationToken)
    {
        var cached = _cached;
        if (cached is not null && _timeProvider.GetUtcNow() - _cachedAt < CacheLifetime)
        {
            return cached;
        }

        return await RefreshAsync(cancellationToken);
    }

    /// <summary>Forces a re-fetch regardless of cache age.</summary>
    public async Task<JsonWebKeySet> RefreshAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = await httpClient.GetStringAsync(JwksEndpoint, cancellationToken);
            var jwks = new JsonWebKeySet(json);
            _cached = jwks;
            _cachedAt = _timeProvider.GetUtcNow();
            return jwks;
        }
        finally
        {
            _lock.Release();
        }
    }
}
