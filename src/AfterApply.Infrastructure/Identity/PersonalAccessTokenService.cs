using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Identity;

internal sealed class PersonalAccessTokenService(AppDbContext dbContext, ITokenService tokenService) : IPersonalAccessTokenService
{
    public async Task<CreatedPersonalAccessTokenResponse> CreateAsync(Guid userId, CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
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
        return true;
    }

    public async Task<Guid?> ValidateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashPersonalAccessToken(rawToken);

        var token = await dbContext.PersonalAccessTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null, cancellationToken);

        if (token is null)
        {
            return null;
        }

        token.RecordUsage(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return token.UserId;
    }
}
