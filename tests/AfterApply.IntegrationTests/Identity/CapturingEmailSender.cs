using AfterApply.Application.JobSources;
using AfterApply.Application.Mailing;
using AfterApply.Application.Payments;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>Stands in for Resend: every e-mail the app sends lands here, so a test can assert it
/// was sent and pull the real token out of a reset link. One instance serves a whole class;
/// <see cref="Reset" /> empties it between tests.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public void Reset()
    {
        LastResetLink = null;
        LastLocale = null;
        PasswordChangedCount = 0;
        lock (Digests) Digests.Clear();
        lock (Receipts) Receipts.Clear();
        lock (ExpiryReminders) ExpiryReminders.Clear();
        lock (RefundsCompleted) RefundsCompleted.Clear();
        lock (RefundsRejected) RefundsRejected.Clear();
        lock (VerificationCodes) VerificationCodes.Clear();
    }

    public string? LastResetLink { get; private set; }

    public string? LastLocale { get; private set; }

    public int PasswordChangedCount { get; private set; }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string locale, CancellationToken cancellationToken)
    {
        LastResetLink = resetLink;
        LastLocale = locale;
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedEmailAsync(string toEmail, string locale, CancellationToken cancellationToken)
    {
        PasswordChangedCount++;
        LastLocale = locale;
        return Task.CompletedTask;
    }

    public List<(string ToEmail, string Code, string Locale)> VerificationCodes { get; } = [];

    /// <summary>The last code sent to <paramref name="email"/>, or null.</summary>
    public string? LastCodeFor(string email)
    {
        lock (VerificationCodes)
        {
            return VerificationCodes.LastOrDefault(c => c.ToEmail == email).Code;
        }
    }

    public Task SendEmailVerificationCodeAsync(string toEmail, string code, string locale, CancellationToken cancellationToken)
    {
        lock (VerificationCodes)
        {
            VerificationCodes.Add((toEmail, code, locale));
        }

        return Task.CompletedTask;
    }

    public List<(string ToEmail, string Locale, WeeklyJobsDigest Digest)> Digests { get; } = [];

    public Task SendWeeklyJobsReadyEmailAsync(string toEmail, string locale, WeeklyJobsDigest digest, CancellationToken cancellationToken)
    {
        lock (Digests)
        {
            Digests.Add((toEmail, locale, digest));
        }

        return Task.CompletedTask;
    }

    public List<(string ToEmail, string Locale, PaymentReceipt Receipt)> Receipts { get; } = [];

    public List<(string ToEmail, string Locale, string ActiveUntil, string RenewLink)> ExpiryReminders { get; } = [];

    public List<(string ToEmail, string Locale, string Amount)> RefundsCompleted { get; } = [];

    public List<(string ToEmail, string Locale, string Note)> RefundsRejected { get; } = [];

    public Task SendPaymentReceivedEmailAsync(string toEmail, string locale, PaymentReceipt receipt, CancellationToken cancellationToken)
    {
        lock (Receipts)
        {
            Receipts.Add((toEmail, locale, receipt));
        }

        return Task.CompletedTask;
    }

    public Task SendProExpiringEmailAsync(string toEmail, string locale, string activeUntilText, string renewLink, CancellationToken cancellationToken)
    {
        lock (ExpiryReminders)
        {
            ExpiryReminders.Add((toEmail, locale, activeUntilText, renewLink));
        }

        return Task.CompletedTask;
    }

    public Task SendRefundCompletedEmailAsync(string toEmail, string locale, string amountText, CancellationToken cancellationToken)
    {
        lock (RefundsCompleted)
        {
            RefundsCompleted.Add((toEmail, locale, amountText));
        }

        return Task.CompletedTask;
    }

    public Task SendRefundRejectedEmailAsync(string toEmail, string locale, string note, CancellationToken cancellationToken)
    {
        lock (RefundsRejected)
        {
            RefundsRejected.Add((toEmail, locale, note));
        }

        return Task.CompletedTask;
    }
}
