namespace AfterApply.Infrastructure.Identity;

public enum AuthEmailKind
{
    EmailVerification,
    PasswordReset
}

/// <summary>
/// One account email we sent (or are about to): the ledger AuthEmailThrottle counts, per account
/// and in total, before it lets another one go. Kept two days, then purged — long enough for the
/// daily windows, no longer.
/// </summary>
public sealed class AuthEmailDispatch
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public AuthEmailKind Kind { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    private AuthEmailDispatch()
    {
    }

    public static AuthEmailDispatch Create(Guid userId, AuthEmailKind kind, DateTimeOffset now) =>
        new() { UserId = userId, Kind = kind, SentAt = now };
}
