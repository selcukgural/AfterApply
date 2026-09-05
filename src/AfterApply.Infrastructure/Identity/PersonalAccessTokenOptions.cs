namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Limits on browser-extension access tokens, bound from the <c>PersonalAccessTokens</c> section.
/// Defaults are the previously hardcoded values. Both are published through <c>GET /api/config</c>
/// so the settings page and its messages state the real limit instead of a copy of it.
/// </summary>
public sealed class PersonalAccessTokenOptions
{
    public const string SectionName = "PersonalAccessTokens";

    /// <summary>Active (not revoked, not expired) tokens a user may hold at once.</summary>
    public int MaxActiveTokens { get; init; } = 10;

    /// <summary>Long enough not to be an ongoing chore (the extension has no refresh mechanism —
    /// expiry means the user pastes a new token), short enough that a token leaked from a browser
    /// profile has a bounded life. Matches the common default for CI/API tokens elsewhere.</summary>
    public int LifetimeDays { get; init; } = 90;

    public TimeSpan Lifetime => TimeSpan.FromDays(LifetimeDays);
}
