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
}
