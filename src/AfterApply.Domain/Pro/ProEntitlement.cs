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

    /// <summary>The <see cref="ActiveUntil"/> the "your Pro period is ending" e-mail was sent for.
    /// Compared, not cleared: a later extension moves <see cref="ActiveUntil"/> and the new period
    /// earns its own reminder without anyone touching this.</summary>
    public DateTimeOffset? ExpiryReminderSentFor { get; private set; }

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

    /// <summary>A refund took back the period one order had added. The end simply moves earlier
    /// by that much: if it lands in the past the entitlement stops being active, and days from
    /// earlier periods that were paid for and not refunded are kept.</summary>
    public void WindBack(TimeSpan by)
    {
        if (by > TimeSpan.Zero)
        {
            ActiveUntil -= by;
        }
    }

    public void MarkExpiryReminderSent() => ExpiryReminderSentFor = ActiveUntil;

    public bool NeedsExpiryReminder(DateTimeOffset now, TimeSpan window) =>
        IsActive(now) && ActiveUntil <= now + window && ExpiryReminderSentFor != ActiveUntil;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ActiveUntil > now;
}

public enum ProEntitlementSource
{
    Manual,
    PayTr
}
