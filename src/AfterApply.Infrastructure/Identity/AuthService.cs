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
            CreatedAt = DateTimeOffset.UtcNow,
            ConsentAcceptedAt = DateTimeOffset.UtcNow
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

    public async Task<bool> DeleteAccountAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        var checkResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!checkResult.Succeeded)
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Applications cascade to ApplicationEvents/ApplicationStatusHistories/Reminders/
        // EmailSuggestions (FK'd to Application, not User); ImportBatches cascade to
        // ImportRowErrors. Companies/Jobs are shared/global (no UserId) and are never
        // touched. UserManager.DeleteAsync cascades RefreshTokens and EmailConnections
        // (FK'd directly to Users) — any EmailSuggestions are already gone by then via the
        // Application-cascade above, so no separate EmailConnections/EmailSuggestions
        // deletion step is needed here.
        await dbContext.Applications.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ImportBatches.Where(b => b.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AccountExportResponse> ExportAccountDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Authenticated user not found.");

        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id,
                (a, c) => new { a.Id, CompanyName = c.Name, a.JobTitle, a.Status, a.AppliedAt, a.CreatedAt, a.UpdatedAt })
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(a => a.Id).ToList();

        var events = await dbContext.ApplicationEvents
            .Where(e => applicationIds.Contains(e.ApplicationId))
            .Select(e => new { e.ApplicationId, e.Type, e.OccurredAt, e.Source, e.Metadata })
            .ToListAsync(cancellationToken);
        var eventsByApplication = events.ToLookup(e => e.ApplicationId);

        var history = await dbContext.ApplicationStatusHistories
            .Where(h => applicationIds.Contains(h.ApplicationId))
            .Select(h => new { h.ApplicationId, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note })
            .ToListAsync(cancellationToken);
        var historyByApplication = history.ToLookup(h => h.ApplicationId);

        var applicationItems = applications
            .Select(a => new ApplicationExportItem(
                a.Id, a.CompanyName, a.JobTitle, a.Status, a.AppliedAt, a.CreatedAt, a.UpdatedAt,
                eventsByApplication[a.Id]
                    .Select(e => new ApplicationEventExportItem(e.Type, e.OccurredAt, e.Source, e.Metadata))
                    .ToList(),
                historyByApplication[a.Id]
                    .Select(h => new StatusHistoryExportItem(h.FromStatus, h.ToStatus, h.ChangedAt, h.Note))
                    .ToList()))
            .ToList();

        var importBatches = await dbContext.ImportBatches
            .Where(b => b.UserId == userId)
            .Select(b => new ImportBatchExportItem(b.Id, b.Source, b.FileName, b.TotalRecords, b.NewApplications, b.CompletedAt))
            .ToListAsync(cancellationToken);

        var reminders = await dbContext.Reminders
            .Where(r => r.UserId == userId)
            .Select(r => new ReminderExportItem(r.Id, r.ApplicationId, r.Type, r.ReferenceAt, r.CreatedAt, r.DismissedAt))
            .ToListAsync(cancellationToken);

        return new AccountExportResponse(ToProfile(user), applicationItems, importBatches, reminders, DateTimeOffset.UtcNow);
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
        new(user.Id, user.Email!, user.FirstName, user.LastName, user.CreatedAt, user.ConsentAcceptedAt);
}
