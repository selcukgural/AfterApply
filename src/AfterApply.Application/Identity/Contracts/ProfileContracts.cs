namespace AfterApply.Application.Identity.Contracts;

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record UpdateLanguageRequest(string Language);

public sealed record UpdateThemeRequest(string Theme);

/// <summary><paramref name="HasPassword"/> is false for an account created through Google sign-in
/// that never set a password — the settings page uses it to skip the "re-enter your password"
/// field on account deletion, since there is nothing to re-enter.
///
/// <para><paramref name="IsAdmin"/> is the caller's own flag, and it exists so the web app can show
/// the admin link to the handful of accounts it belongs to instead of leaving those pages reachable
/// by typed URL only. It is a rendering hint and never an authorisation decision: every /api/admin
/// endpoint re-reads Users.IsAdmin from the database on each request (see AdminAccessService), so a
/// client that flips this to true in memory gains nothing but a link that 403s.</para></summary>
public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConsentAcceptedAt,
    string PreferredLanguage,
    string PreferredTheme,
    bool HasPassword,
    bool IsAdmin);
