using AfterApply.Application.Common;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AfterApply.Infrastructure.Identity;

internal sealed class PersonalAccessTokenService(AppDbContext dbContext, ITokenService tokenService, HybridCache cache) : IPersonalAccessTokenService
{
    private const int MaxActiveTokens = 10;

    // Was 60s. RevokeAsync evicts this key, but HybridCache's in-process L1 has no backplane here,
    // so a revocation only reaches the instance that served the revoke request — every other Cloud
    // Run instance keeps honoring the token until its own L1 entry lapses. That window is the real
    // revocation latency, so it's kept short. 15s still absorbs the burst this cache exists for (a
    // Gmail content script firing several requests as the user moves through threads) while cutting
    // the worst-case "revoked token still works" window to a quarter of what it was.
    private static readonly HybridCacheEntryOptions ValidationCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(15),
        LocalCacheExpiration = TimeSpan.FromSeconds(15)
    };

    private static string ValidationCacheKey(string tokenHash) => $"pat:{tokenHash}";

    public async Task<CreatedPersonalAccessTokenResponse> CreateAsync(Guid userId, CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Expired tokens no longer count against the cap — otherwise a user who let ten tokens
        // lapse could never create an eleventh without hunting down and revoking dead rows.
        var activeCount = await dbContext.PersonalAccessTokens
            .CountAsync(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now, cancellationToken);
        if (activeCount >= MaxActiveTokens)
        {
            throw new CodedException("PERSONAL_ACCESS_TOKEN_LIMIT_REACHED",
                "You can have at most 10 active personal access tokens.");
        }

        var rawToken = tokenService.GeneratePersonalAccessToken();
        var tokenHash = tokenService.HashPersonalAccessToken(rawToken);

        var token = PersonalAccessToken.Create(userId, request.Name, tokenHash, request.Scope, now);
        dbContext.PersonalAccessTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedPersonalAccessTokenResponse(
            token.Id, token.Name, rawToken, token.Scope, token.CreatedAt, token.ExpiresAt);
    }

    public async Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return await dbContext.PersonalAccessTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PersonalAccessTokenResponse(t.Id, t.Name, t.Scope, t.CreatedAt, t.ExpiresAt, t.LastUsedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var token = await dbContext.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token is null || !token.IsActiveAt(now))
        {
            return false;
        }

        token.Revoke(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ValidationCacheKey(token.TokenHash), cancellationToken);
        return true;
    }

    public async Task<ValidatedPersonalAccessToken?> ValidateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashPersonalAccessToken(rawToken);

        // Cache-aside on the tokenHash->identity mapping: a hit skips both the SELECT and the
        // RecordUsage UPDATE below, so LastUsedAt only goes stale by up to the cache TTL. That's
        // the point of caching this path — it's on every single extension-authenticated request.
        var cached = await cache.GetOrCreateAsync<ValidatedPersonalAccessToken?>(
            ValidationCacheKey(tokenHash),
            async ct =>
            {
                var now = DateTimeOffset.UtcNow;

                var token = await dbContext.PersonalAccessTokens
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null && t.ExpiresAt > now, ct);

                if (token is null)
                {
                    return null;
                }

                token.RecordUsage(now);
                await dbContext.SaveChangesAsync(ct);

                return new ValidatedPersonalAccessToken(token.UserId, token.Scope);
            },
            ValidationCacheOptions,
            cancellationToken: cancellationToken);

        return cached;
    }
}
