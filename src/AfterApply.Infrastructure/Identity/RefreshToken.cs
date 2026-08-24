namespace AfterApply.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    private RefreshToken()
    {
    }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt,
        DateTimeOffset now, string? createdByIp)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            CreatedByIp = createdByIp
        };
    }

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
