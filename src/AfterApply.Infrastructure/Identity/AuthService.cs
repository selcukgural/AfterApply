using System.Globalization;
using System.Text;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IGoogleAuthClient googleAuthClient,
    AppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IOptions<AppOptions> appOptions,
    IBackgroundJobClient jobClient,
    IdentityErrorDescriber errorDescriber,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly AppOptions _appOptions = appOptions.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTimeOffset.UtcNow,
            ConsentAcceptedAt = DateTimeOffset.UtcNow,
            // Whatever locale RequestLocalization resolved from this request's Accept-Language
            // (i.e. whichever UI language the visitor was on when they registered) becomes the
            // account's initial preference, so it's already correct before they ever touch the
            // language switcher.
            PreferredLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Descriptions are already localized here: LocalizedIdentityErrorDescriber (registered via
            // .AddErrorDescriber<LocalizedIdentityErrorDescriber>()) formats them using the request's
            // current culture, args included — unlike AUTH_INVALID_CREDENTIALS/AUTH_INVALID_REFRESH_TOKEN
            // below, which are bare codes translated later at the API boundary (no args to carry).
            return AuthResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS");
        }

        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, cancellationToken));
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return AuthResult.Failure("AUTH_INVALID_REFRESH_TOKEN");
        }

        if (!stored.IsActive)
        {
            await RevokeAllActiveTokensAsync(stored.UserId, cancellationToken);
            return AuthResult.Failure("AUTH_INVALID_REFRESH_TOKEN");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            return AuthResult.Failure("AUTH_INVALID_REFRESH_TOKEN");
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

    public async Task<GoogleSignInResult> GoogleSignInAsync(GoogleSignInRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        // Google itself refuses a redirect_uri that isn't registered on the OAuth client, so this
        // is belt-and-braces: it keeps a caller from making us exchange a code that was minted for
        // some other site's callback, and it fails before the round-trip rather than after.
        if (!IsOurWebOrigin(request.RedirectUri))
        {
            logger.LogWarning("Google sign-in rejected: redirect URI {RedirectUri} is not under App:WebBaseUrl", request.RedirectUri);
            return GoogleSignInResult.Failure("AUTH_GOOGLE_FAILED");
        }

        var identity = await googleAuthClient.ExchangeCodeAsync(request.Code, request.CodeVerifier, request.RedirectUri, cancellationToken);
        if (identity is null)
        {
            return GoogleSignInResult.Failure("AUTH_GOOGLE_FAILED");
        }

        // The email is what links a Google identity to an existing account (and what a new account
        // is created under), so an address Google hasn't verified must not get either — it would let
        // anyone claim a Workspace/legacy Google account under someone else's address.
        if (!identity.EmailVerified)
        {
            return GoogleSignInResult.Failure("AUTH_GOOGLE_EMAIL_NOT_VERIFIED");
        }

        var user = await FindOrLinkGoogleUserAsync(identity);
        if (user is not null)
        {
            return GoogleSignInResult.SignedIn(await IssueTokensAsync(user, ipAddress, cancellationToken));
        }

        // New to us: no account yet. The privacy-policy consent a password sign-up collects on its
        // form has to be collected here too, and the code is already spent, so hand the client a
        // signed carrier for the verified identity to bring back with the consent.
        var signupToken = tokenService.CreateGoogleSignupToken(identity);
        return GoogleSignInResult.SignupRequired(new GoogleSignupPrefill(
            signupToken, identity.Email, identity.GivenName ?? string.Empty, identity.FamilyName ?? string.Empty));
    }

    public async Task<AuthResult> CompleteGoogleSignupAsync(GoogleSignupRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var identity = tokenService.ValidateGoogleSignupToken(request.SignupToken);
        if (identity is null || !identity.EmailVerified)
        {
            return AuthResult.Failure("AUTH_GOOGLE_SIGNUP_EXPIRED");
        }

        // Replay or a race with another tab: the account exists now, so behave like a sign-in.
        var existing = await FindOrLinkGoogleUserAsync(identity);
        if (existing is not null)
        {
            return AuthResult.Success(await IssueTokensAsync(existing, ipAddress, cancellationToken));
        }

        var user = new ApplicationUser
        {
            UserName = identity.Email,
            Email = identity.Email,
            // Google vouched for the address — the one thing a password sign-up can't say yet
            // (see DECISIONS.md "E-posta doğrulaması bilinçli olarak ertelendi").
            EmailConfirmed = true,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTimeOffset.UtcNow,
            ConsentAcceptedAt = DateTimeOffset.UtcNow,
            PreferredLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        // UserManager shares AppDbContext, so one transaction covers both the user row and its
        // login row — an account must never exist without the Google link that created it, or the
        // next Google sign-in would take the by-email path and "link" it a second time.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            return AuthResult.Failure(created.Errors.Select(e => e.Description).ToArray());
        }

        var linked = await userManager.AddLoginAsync(user, GoogleLogin(identity));
        if (!linked.Succeeded)
        {
            return AuthResult.Failure(linked.Errors.Select(e => e.Description).ToArray());
        }

        var response = await IssueTokensAsync(user, ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("User {UserId} registered via Google sign-in", user.Id);
        return AuthResult.Success(response);
    }

    /// <summary>The account for a Google identity, if there is one: by the stored Google subject first,
    /// then by verified email — in which case the Google login is attached to that account on the way
    /// out, so the next sign-in hits the first branch. Null means "no account yet".</summary>
    private async Task<ApplicationUser?> FindOrLinkGoogleUserAsync(GoogleIdentity identity)
    {
        var user = await userManager.FindByLoginAsync(GoogleAuthOptions.LoginProvider, identity.Subject);
        if (user is not null)
        {
            return user;
        }

        user = await userManager.FindByEmailAsync(identity.Email);
        if (user is null)
        {
            return null;
        }

        // Auto-link on a verified email (DECISIONS.md, 2026-09-05). Password sign-ups never verified
        // their address; Google just did, so record that too.
        var linked = await userManager.AddLoginAsync(user, GoogleLogin(identity));
        if (!linked.Succeeded)
        {
            // Only LoginAlreadyAssociated can fail here, which means a concurrent request linked it
            // first — the account is the same either way.
            logger.LogWarning("Linking Google login to user {UserId} failed: {Errors}", user.Id,
                string.Join(", ", linked.Errors.Select(e => e.Code)));
            return user;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        logger.LogInformation("Linked Google login to existing user {UserId} by verified email", user.Id);
        return user;
    }

    private bool IsOurWebOrigin(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var candidate)
            || !Uri.TryCreate(_appOptions.WebBaseUrl, UriKind.Absolute, out var webBase))
        {
            return false;
        }

        return Uri.Compare(candidate, webBase, UriComponents.SchemeAndServer, UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static UserLoginInfo GoogleLogin(GoogleIdentity identity) =>
        new(GoogleAuthOptions.LoginProvider, identity.Subject, GoogleAuthOptions.LoginProvider);

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Deliberately no-op — never reveal whether this email is registered. The endpoint
            // returns the same generic response either way.
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var resetLink = $"{_appOptions.WebBaseUrl}/{locale}/reset-password" +
                         $"?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(encodedToken)}";

        // Enqueued, not awaited: keeps this response's timing identical to the "no such user"
        // branch above regardless of how long Resend takes, and gets Hangfire's automatic retry
        // (10 attempts, backoff) for free on a transient send failure — see ResendEmailSender.
        var email = user.Email!;
        jobClient.Enqueue<IEmailSender>(s => s.SendPasswordResetEmailAsync(email, resetLink, locale, CancellationToken.None));
        logger.LogInformation("Password reset requested for user {UserId} from {Ip}", user.Id, ipAddress);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Same wording Identity itself would return for a genuinely invalid/expired token
            // (reuses the registered IdentityErrorDescriber, localized) — an attacker probing
            // arbitrary emails can't distinguish "no such account" from "bad token".
            return PasswordResetResult.Failure(errorDescriber.InvalidToken().Description);
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return PasswordResetResult.Failure(errorDescriber.InvalidToken().Description);
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            // Errors here are already localized IdentityError.Description values (either the same
            // InvalidToken text as above for an expired/tampered token, or a specific password-policy
            // message) — safe to surface as-is, same as RegisterAsync below.
            return PasswordResetResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        // Force logout everywhere: a password reset is exactly the moment a stolen session should
        // stop working, on every device, not just the one completing the reset.
        await RevokeAllActiveTokensAsync(user.Id, cancellationToken);

        var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var email = user.Email!;
        jobClient.Enqueue<IEmailSender>(s => s.SendPasswordChangedEmailAsync(email, locale, CancellationToken.None));
        logger.LogInformation("Password reset completed for user {UserId}", user.Id);

        return PasswordResetResult.Success();
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

    public async Task<UserProfileResponse?> UpdateLanguageAsync(Guid userId, string language, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        user.PreferredLanguage = language;
        await userManager.UpdateAsync(user);

        return ToProfile(user);
    }

    public async Task<UserProfileResponse?> UpdateThemeAsync(Guid userId, string theme, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        user.PreferredTheme = theme;
        await userManager.UpdateAsync(user);

        return ToProfile(user);
    }

    public async Task<bool> DeleteAccountAsync(Guid userId, string? password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        // A Google-only account has no password to re-enter (DECISIONS.md, 2026-09-05). For every
        // other account the re-check stays: a missing password is simply a wrong one.
        if (await userManager.HasPasswordAsync(user))
        {
            var checkResult = await signInManager.CheckPasswordSignInAsync(user, password ?? string.Empty, lockoutOnFailure: true);
            if (!checkResult.Succeeded)
            {
                return false;
            }
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
        new(user.Id, user.Email!, user.FirstName, user.LastName, user.CreatedAt, user.ConsentAcceptedAt,
            user.PreferredLanguage, user.PreferredTheme, HasPassword: user.PasswordHash is not null);
}
