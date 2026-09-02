namespace AfterApply.Application.Mailing;

/// <summary>Outbound transactional email — distinct from AfterApply.Application.EmailIntegrations,
/// which is the inbound Cloudflare-forwarding pipeline (users forwarding job emails to us).
/// Implementations are called from a Hangfire background job (see AuthService), not inline within
/// the triggering HTTP request, so there's no ambient request culture to read — callers must pass
/// the target locale ("tr"/"en") explicitly.</summary>
public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string locale, CancellationToken cancellationToken);

    Task SendPasswordChangedEmailAsync(string toEmail, string locale, CancellationToken cancellationToken);
}
