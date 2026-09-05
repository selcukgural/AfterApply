namespace AfterApply.Application.Identity.Contracts;

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record UpdateLanguageRequest(string Language);

public sealed record UpdateThemeRequest(string Theme);

/// <summary><paramref name="HasPassword"/> is false for an account created through Google sign-in
/// that never set a password — the settings page uses it to skip the "re-enter your password"
/// field on account deletion, since there is nothing to re-enter.</summary>
public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConsentAcceptedAt,
    string PreferredLanguage,
    string PreferredTheme,
    bool HasPassword);
