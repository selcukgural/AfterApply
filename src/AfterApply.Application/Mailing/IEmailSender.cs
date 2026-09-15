namespace AfterApply.Application.Mailing;

/// <summary>Outbound transactional email — distinct from AfterApply.Application.EmailIntegrations,
/// which processes recruitment-signal emails read by the browser extension's Gmail content script.
/// Implementations are called from a Hangfire background job (see AuthService), not inline within
/// the triggering HTTP request, so there's no ambient request culture to read — callers must pass
/// the target locale ("tr"/"en") explicitly.</summary>
public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string locale, CancellationToken cancellationToken);

    Task SendPasswordChangedEmailAsync(string toEmail, string locale, CancellationToken cancellationToken);

    /// <summary>The Monday digest of the weekly job matching. Values in <paramref name="digest"/>
    /// that came from a job site (title, company) are HTML-encoded by the sender before they
    /// reach the template.</summary>
    Task SendWeeklyJobsReadyEmailAsync(string toEmail, string locale, WeeklyJobsDigest digest, CancellationToken cancellationToken);

    /// <summary>PayTR confirmed a Pro payment. Amount and date are pre-formatted in the user's
    /// locale by the caller; the link is ours.</summary>
    Task SendPaymentReceivedEmailAsync(string toEmail, string locale, PaymentReceipt receipt, CancellationToken cancellationToken);

    /// <summary>The prepaid Pro period ends in a few days and does not renew by itself.</summary>
    Task SendProExpiringEmailAsync(string toEmail, string locale, string activeUntilText, string renewLink, CancellationToken cancellationToken);

    Task SendRefundCompletedEmailAsync(string toEmail, string locale, string amountText, CancellationToken cancellationToken);

    /// <summary><paramref name="note"/> is text an admin typed; the sender HTML-encodes it.</summary>
    Task SendRefundRejectedEmailAsync(string toEmail, string locale, string note, CancellationToken cancellationToken);
}

public sealed record PaymentReceipt(string PlanName, string AmountText, string ActiveUntilText, string OrdersLink);

/// <summary>What the digest says: how many postings the list shows this week and, when there is
/// a scored one, the best of them. <paramref name="Link"/> is the weekly-jobs page in the user's locale.</summary>
public sealed record WeeklyJobsDigest(int Count, string? BestTitle, string? BestCompany, int? BestScore, string Link);
