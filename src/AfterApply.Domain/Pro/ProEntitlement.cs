using AfterApply.Domain.Common;

namespace AfterApply.Domain.Pro;

/// <summary>
/// Whether a user is on the paid plan, and until when. The smallest record that lets the weekly
/// job answer "is this user paying?" before there is a payment provider: today an admin writes it
/// by hand (<see cref="ProEntitlementSource.Manual"/>); when the payment integration lands it
/// writes the same row with its own source and keeps <see cref="ActiveUntil"/> rolling. One row
/// per user; goes with the account.
/// </summary>
public sealed class ProEntitlement : Entity
{
    public Guid UserId { get; private set; }

    public DateTimeOffset ActiveUntil { get; private set; }

    public ProEntitlementSource Source { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private ProEntitlement()
    {
    }

    public static ProEntitlement Grant(Guid userId, DateTimeOffset activeUntil, ProEntitlementSource source, DateTimeOffset now) =>
        new() { UserId = userId, ActiveUntil = activeUntil, Source = source, GrantedAt = now };

    public void Extend(DateTimeOffset activeUntil, ProEntitlementSource source, DateTimeOffset now)
    {
        ActiveUntil = activeUntil;
        Source = source;
        GrantedAt = now;
        RevokedAt = null;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt = now;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ActiveUntil > now;
}

public enum ProEntitlementSource
{
    Manual,
    PayTr
}
