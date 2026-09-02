namespace AfterApply.Application.Identity.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, bool ConsentAccepted);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileResponse User);

public sealed record AuthResult
{
    public bool Succeeded { get; private init; }

    public AuthResponse? Response { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = [];

    public static AuthResult Success(AuthResponse response) => new() { Succeeded = true, Response = response };

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
