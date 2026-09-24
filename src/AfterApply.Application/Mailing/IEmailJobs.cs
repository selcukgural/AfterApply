namespace AfterApply.Application.Mailing;

/// <summary>
/// The background jobs behind the account emails. Hangfire stores a job's arguments as plain text
/// for as long as the job row lives — a failed one until someone deletes it — so these take the
/// account id and look the address up when they run, and the password-reset job mints its token
/// then too. A job that outlives its account finds nobody and sends nothing.
/// </summary>
public interface IAccountEmailJobs
{
    Task SendPasswordResetAsync(Guid userId, string locale, CancellationToken cancellationToken);

    Task SendPasswordChangedAsync(Guid userId, string locale, CancellationToken cancellationToken);
}

/// <summary>The payment emails, keyed by order (or, for the expiry reminder, account) for the same
/// reason as <see cref="IAccountEmailJobs"/>: the address and the admin's note stay in their rows.</summary>
public interface IPaymentEmailJobs
{
    Task SendReceiptAsync(Guid orderId, DateTimeOffset activeUntil, CancellationToken cancellationToken);

    Task SendRefundCompletedAsync(Guid orderId, long amountMinor, CancellationToken cancellationToken);

    Task SendRefundRejectedAsync(Guid orderId, CancellationToken cancellationToken);

    Task SendProExpiringAsync(Guid userId, DateTimeOffset activeUntil, CancellationToken cancellationToken);
}
