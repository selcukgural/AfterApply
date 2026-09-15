namespace AfterApply.Application.Payments;

/// <summary>The two Hangfire jobs of the payment feature. Both idempotent — a run that finds
/// nothing to do is the normal case.</summary>
public interface IPaymentMaintenanceService
{
    /// <summary>Pending orders whose payment window closed (plus grace) with no notification → Expired.</summary>
    Task<int> ExpirePendingOrdersAsync(CancellationToken cancellationToken);

    /// <summary>Entitlements ending within the reminder window that have not been reminded for
    /// this end date → one "your Pro period is ending" e-mail each.</summary>
    Task<int> SendExpiryRemindersAsync(CancellationToken cancellationToken);
}
