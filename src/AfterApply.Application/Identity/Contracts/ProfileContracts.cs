namespace AfterApply.Application.Identity.Contracts;

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record UpdateLanguageRequest(string Language);

public sealed record UpdateThemeRequest(string Theme);

/// <summary>The caller's own Pro status for the profile page, readable whether or not the checkout
/// or weekly job matching is switched on — the two other readers of the entitlement
/// (/api/payments/plans, /api/job-sources/status) 404 behind their flags. Deliberately the minimum:
/// <paramref name="ActiveUntil"/> stays set after the period ends so the page can say "Pro ended on…";
/// how the period was granted (manual, PayTR) or revoked is not the user's concern here.</summary>
public sealed record UserPlanResponse(bool IsActive, DateTimeOffset? ActiveUntil);

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
