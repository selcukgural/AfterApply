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

    private static readonly HybridCacheEntryOptions ValidationCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60)
    };

    private static string ValidationCacheKey(string tokenHash) => $"pat:{tokenHash}";

    public async Task<CreatedPersonalAccessTokenResponse> CreateAsync(Guid userId, CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var activeCount = await dbContext.PersonalAccessTokens
            .CountAsync(t => t.UserId == userId && t.RevokedAt == null, cancellationToken);
        if (activeCount >= MaxActiveTokens)
        {
            throw new CodedException("PERSONAL_ACCESS_TOKEN_LIMIT_REACHED",
                "You can have at most 10 active personal access tokens.");
        }

        var now = DateTimeOffset.UtcNow;
        var rawToken = tokenService.GeneratePersonalAccessToken();
        var tokenHash = tokenService.HashPersonalAccessToken(rawToken);

        var token = PersonalAccessToken.Create(userId, request.Name, tokenHash, now);
        dbContext.PersonalAccessTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedPersonalAccessTokenResponse(token.Id, token.Name, rawToken, token.CreatedAt);
    }

    public async Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.PersonalAccessTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PersonalAccessTokenResponse(t.Id, t.Name, t.CreatedAt, t.LastUsedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
    {
        var token = await dbContext.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token is null || !token.IsActive)
        {
            return false;
        }

        token.Revoke(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ValidationCacheKey(token.TokenHash), cancellationToken);
        return true;
    }

    public async Task<Guid?> ValidateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashPersonalAccessToken(rawToken);

        // Cache-aside on the tokenHash->userId mapping: a hit skips both the SELECT and the
        // RecordUsage UPDATE below, so LastUsedAt only goes stale by up to the cache TTL. That's
        // the point of caching this path — it's on every single extension-authenticated request.
        return await cache.GetOrCreateAsync<Guid?>(
            ValidationCacheKey(tokenHash),
            async ct =>
            {
                var token = await dbContext.PersonalAccessTokens
                    .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null, ct);

                if (token is null)
                {
                    return null;
                }

                token.RecordUsage(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(ct);

                return token.UserId;
            },
            ValidationCacheOptions,
            cancellationToken: cancellationToken);
    }
}
