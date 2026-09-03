using AfterApply.Application.Identity;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// A long-lived, user-revocable credential for non-browser clients (the browser extension —
/// Sprint 9) that can't hold the web app's short-lived JWT/refresh-token pair (Sprint 2's
/// localStorage + single-flight refresh design assumes a browser session).
///
/// Sprint 9 issued these unscoped and non-expiring on purpose (see DECISIONS.md). The 2026-09-03
/// security pass reversed both halves of that: a token that never expires and can do everything the
/// account can is a poor fit for a credential that lives in chrome.storage.local and is read into a
/// content script on every mail.google.com page load. Tokens now carry a
/// <see cref="PersonalAccessTokenScope"/> and a hard expiry; existing rows were backfilled as
/// Full-scoped so nothing already deployed broke.
/// </summary>
public sealed class PersonalAccessToken
{
    /// <summary>Long enough not to be an ongoing chore (the extension has no refresh mechanism —
    /// expiry means the user pastes a new token), short enough that a token leaked from a browser
    /// profile has a bounded life. Matches the common default for CI/API tokens elsewhere.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(90);

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public PersonalAccessTokenScope Scope { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    private PersonalAccessToken()
    {
    }

    public static PersonalAccessToken Create(
        Guid userId, string name, string tokenHash, PersonalAccessTokenScope scope, DateTimeOffset now)
    {
        return new PersonalAccessToken
        {
            UserId = userId,
            Name = name,
            TokenHash = tokenHash,
            Scope = scope,
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
    }

    public void RecordUsage(DateTimeOffset now)
    {
        LastUsedAt = now;
    }
}
