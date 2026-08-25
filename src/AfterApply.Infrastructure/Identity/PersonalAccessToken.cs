namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// A long-lived, user-revocable credential for non-browser clients (the browser extension —
/// Sprint 9) that can't hold the web app's short-lived JWT/refresh-token pair (Sprint 2's
/// localStorage + single-flight refresh design assumes a browser session). v1 is deliberately
/// unscoped — same access as a JWT session for the owning user — see DECISIONS.md Sprint 9.
/// </summary>
public sealed class PersonalAccessToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    private PersonalAccessToken()
    {
    }

    public static PersonalAccessToken Create(Guid userId, string name, string tokenHash, DateTimeOffset now)
    {
        return new PersonalAccessToken
        {
            UserId = userId,
            Name = name,
            TokenHash = tokenHash,
            CreatedAt = now
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
