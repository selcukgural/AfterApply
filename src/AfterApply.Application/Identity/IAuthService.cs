using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Completes the browser's Google authorization-code flow. Signs the user straight in
    /// when the Google account is already linked, or when its verified email matches an existing
    /// account (which gets linked on the spot); otherwise returns a signup prefill for the
    /// complete-your-sign-up step — no account is created here.</summary>
    Task<GoogleSignInResult> GoogleSignInAsync(GoogleSignInRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Creates the account for a Google identity carried by a signup token from
    /// <see cref="GoogleSignInAsync"/>. Idempotent: if the account already exists by then (the token
    /// was replayed, or a second tab won the race), the user is signed into it instead.</summary>
    Task<AuthResult> CompleteGoogleSignupAsync(GoogleSignupRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Completes the browser's LinkedIn OpenID Connect authorization-code flow. Signs the
    /// user straight in when the LinkedIn account is already linked, or when its verified email
    /// matches an existing account (linked on the spot); otherwise returns a signup prefill — no
    /// account is created here. Unlike <see cref="GoogleSignInAsync"/>, LinkedIn may supply no email
    /// at all, in which case the prefill's email is null and the sign-up step must collect one.</summary>
    Task<LinkedInSignInResult> LinkedInSignInAsync(LinkedInSignInRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Creates the account for a LinkedIn identity carried by a signup token from
    /// <see cref="LinkedInSignInAsync"/>. Idempotent like <see cref="CompleteGoogleSignupAsync"/>.
    /// When the identity itself carried no verified email, <paramref name="request"/>'s <c>Email</c>
    /// is required and used (unconfirmed); otherwise the token's own verified email is used and the
    /// request's is ignored.</summary>
    Task<AuthResult> CompleteLinkedInSignupAsync(LinkedInSignupRequest request, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Always completes successfully regardless of whether the email is registered —
    /// callers must not branch on this to avoid leaking account existence.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<UserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdateLanguageAsync(Guid userId, string language, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdateThemeAsync(Guid userId, string theme, CancellationToken cancellationToken);

    /// <summary>Returns false when the supplied password does not match the account (the account owner is
    /// already established via the authenticated userId, so false unambiguously means "wrong password").
    /// An account without a password (created through Google sign-in) is deleted without one — the
    /// bearer token is the only proof of ownership such an account can offer.</summary>
    Task<bool> DeleteAccountAsync(Guid userId, string? password, CancellationToken cancellationToken);

    Task<AccountExportResponse> ExportAccountDataAsync(Guid userId, CancellationToken cancellationToken);
}
