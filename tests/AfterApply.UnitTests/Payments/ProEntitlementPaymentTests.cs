using AfterApply.Domain.Pro;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

public sealed class ProEntitlementPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Winding_back_moves_the_end_earlier_and_can_deactivate()
    {
        var entitlement = ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(20), ProEntitlementSource.PayTr, Now);

        entitlement.WindBack(TimeSpan.FromDays(10));
        entitlement.ActiveUntil.ShouldBe(Now.AddDays(10));
        entitlement.IsActive(Now).ShouldBeTrue();

        entitlement.WindBack(TimeSpan.FromDays(30));
        entitlement.IsActive(Now).ShouldBeFalse();
    }

    [Fact]
    public void Winding_back_by_nothing_changes_nothing()
    {
        var entitlement = ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(20), ProEntitlementSource.PayTr, Now);

        entitlement.WindBack(TimeSpan.Zero);
        entitlement.WindBack(TimeSpan.FromDays(-5));

        entitlement.ActiveUntil.ShouldBe(Now.AddDays(20));
    }

    [Fact]
    public void Expiry_reminder_is_due_once_per_end_date_inside_the_window()
    {
        var window = TimeSpan.FromDays(3);
        var entitlement = ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(2), ProEntitlementSource.PayTr, Now);

        entitlement.NeedsExpiryReminder(Now, window).ShouldBeTrue();
        entitlement.MarkExpiryReminderSent();
        entitlement.NeedsExpiryReminder(Now, window).ShouldBeFalse();

        // A new period earns a new reminder without anyone clearing the marker.
        entitlement.Extend(Now.AddDays(2).AddMonths(1), ProEntitlementSource.PayTr, Now);
        entitlement.NeedsExpiryReminder(Now, window).ShouldBeFalse();
        entitlement.NeedsExpiryReminder(Now.AddMonths(1), window).ShouldBeTrue();
    }

    [Fact]
    public void No_reminder_for_a_lapsed_revoked_or_far_away_entitlement()
    {
        var window = TimeSpan.FromDays(3);

        ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(-1), ProEntitlementSource.PayTr, Now).NeedsExpiryReminder(Now, window).ShouldBeFalse();
        ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(10), ProEntitlementSource.PayTr, Now).NeedsExpiryReminder(Now, window).ShouldBeFalse();

        var revoked = ProEntitlement.Grant(Guid.NewGuid(), Now.AddDays(1), ProEntitlementSource.Manual, Now);
        revoked.Revoke(Now);
        revoked.NeedsExpiryReminder(Now, window).ShouldBeFalse();
    }
}
