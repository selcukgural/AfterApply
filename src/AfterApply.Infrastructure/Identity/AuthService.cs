using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    AppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return AuthResult.Failure("Invalid email or password.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return AuthResult.Failure("Invalid email or password.");
        }

        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return AuthResult.Failure("Invalid refresh token.");
        }

        if (!stored.IsActive)
        {
            await RevokeAllActiveTokensAsync(stored.UserId, cancellationToken);
            return AuthResult.Failure("Invalid refresh token.");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            return AuthResult.Failure("Invalid refresh token.");
        }

        var now = DateTimeOffset.UtcNow;
        var newRefreshTokenValue = tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = tokenService.HashRefreshToken(newRefreshTokenValue);

        stored.Revoke(now, newRefreshTokenHash);

        var newRefreshToken = RefreshToken.Create(user.Id, newRefreshTokenHash,
            now.AddDays(_jwtOptions.RefreshTokenDays), now, ipAddress);
        dbContext.RefreshTokens.Add(newRefreshToken);

        var (accessToken, accessTokenExpiresAt) = tokenService.CreateAccessToken(user.Id, user.Email!);
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(new AuthResponse(accessToken, accessTokenExpiresAt, newRefreshTokenValue,
            newRefreshToken.ExpiresAt, ToProfile(user)));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.Revoke(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToProfile(user);
    }

    public async Task<UserProfileResponse?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        await userManager.UpdateAsync(user);

        return ToProfile(user);
    }

    private async Task RevokeAllActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(now);
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken)
    {
        var (accessToken, accessTokenExpiresAt) = tokenService.CreateAccessToken(user.Id, user.Email!);
        var refreshTokenValue = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashRefreshToken(refreshTokenValue);
        var now = DateTimeOffset.UtcNow;
        var refreshToken = RefreshToken.Create(user.Id, refreshTokenHash, now.AddDays(_jwtOptions.RefreshTokenDays), now, ipAddress);

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, accessTokenExpiresAt, refreshTokenValue, refreshToken.ExpiresAt, ToProfile(user));
    }

    private static UserProfileResponse ToProfile(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FirstName, user.LastName, user.CreatedAt);
}
