namespace AfterApply.Domain.Payments;

/// <summary>What a PayTR order buys: one prepaid period of the Pro plan. The iFrame API has no
/// subscription, so there is no "renews automatically" — a period is bought, it runs out, the
/// user buys another. Prices live in configuration, not here.</summary>
public enum ProPlan
{
    Monthly,
    Yearly
}

public static class ProPlanPeriod
{
    /// <summary>Where a period bought now ends: one calendar month or year on top of
    /// <paramref name="from"/>. Calendar arithmetic (not 30 days) so "monthly" means the same date
    /// next month, the way people read it on a receipt.</summary>
    public static DateTimeOffset Extend(DateTimeOffset from, ProPlan plan) => plan switch
    {
        ProPlan.Monthly => from.AddMonths(1),
        ProPlan.Yearly => from.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unknown Pro plan.")
    };

    /// <summary>The start a new period is added to: the current end when the entitlement is
    /// still running (buying early never loses days), otherwise now.</summary>
    public static DateTimeOffset NextStart(DateTimeOffset? currentActiveUntil, DateTimeOffset now) =>
        currentActiveUntil is { } until && until > now ? until : now;
}
