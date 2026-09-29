using System.Text.Json.Serialization;

namespace AfterApply.Application.Identity.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, bool ConsentAccepted);

public sealed record LoginRequest(string Email, string Password);

// The refresh token travels in an HttpOnly cookie (see RefreshTokenCookie in the API). The body
// field only exists so a browser still holding a token from the localStorage era can trade it for
// the cookie once; it goes when those tokens have expired (DECISIONS.md 2026-09-27).
public sealed record RefreshRequest(string? RefreshToken = null);

public sealed record LogoutRequest(string? RefreshToken = null);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

// RefreshToken is never serialized: the API moves it into the HttpOnly cookie on the way out, so
// no script on the page can read it.
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    [property: JsonIgnore] string RefreshToken,
    [property: JsonIgnore] DateTimeOffset RefreshTokenExpiresAt,
    UserProfileResponse User);

public sealed record AuthResult
{
    /// <summary>The refresh token was rotated moments ago by a concurrent request (another tab):
    /// not a replay, so nothing is revoked, and the caller retries with the cookie it now holds.</summary>
    public const string RefreshSuperseded = "AUTH_REFRESH_SUPERSEDED";

    public bool Succeeded { get; private init; }

    public AuthResponse? Response { get; private init; }

    /// <summary>Set, with <see cref="Succeeded"/> true and no <see cref="Response"/>, when the
    /// account exists but its email is unverified: no tokens until the emailed code comes back.</summary>
    public EmailVerificationPendingResponse? PendingVerification { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static AuthResult Success(AuthResponse response) => new() { Succeeded = true, Response = response };

    public static AuthResult VerificationRequired(EmailVerificationPendingResponse pending) =>
        new() { Succeeded = true, PendingVerification = pending };

    public static AuthResult Failure(params IReadOnlyCollection<string> errors) => new() { Succeeded = false, Errors = errors };
}

// Deliberately not AuthResult: a password reset never re-issues tokens (the caller must log in
// again with the new password), so there's no AuthResponse to carry on success.
public sealed record PasswordResetResult
{
    public bool Succeeded { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static PasswordResetResult Success() => new() { Succeeded = true };

    public static PasswordResetResult Failure(params IReadOnlyCollection<string> errors) => new() { Succeeded = false, Errors = errors };
}
