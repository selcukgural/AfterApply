using AfterApply.Application.Mailing;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>Stands in for Resend: every e-mail the app sends lands here, so a test can assert it
/// was sent and pull the real token out of a reset link. One instance serves a whole class;
/// <see cref="Reset" /> empties it between tests.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public string? LastResetLink { get; private set; }

    public string? LastLocale { get; private set; }

    public int PasswordChangedCount { get; private set; }

    public void Reset()
    {
        LastResetLink = null;
        LastLocale = null;
        PasswordChangedCount = 0;
    }

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
}
