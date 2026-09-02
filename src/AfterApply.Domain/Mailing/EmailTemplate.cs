using AfterApply.Domain.Common;

namespace AfterApply.Domain.Mailing;

/// <summary>Subject/HTML body for one (Key, Locale) pair, stored in the database on purpose — see
/// EmailTemplateConfiguration's seed data for the shipped defaults. Editing a row takes effect on
/// the very next send, no redeploy required, which a compiled-in string could never offer.
/// HtmlBody may contain the literal placeholder "{{ResetLink}}" (PasswordReset only), substituted
/// by ResendEmailSender before sending.</summary>
public sealed class EmailTemplate : Entity
{
    public EmailTemplateKey Key { get; private set; }

    public string Locale { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    private EmailTemplate()
    {
    }

    public static EmailTemplate Create(EmailTemplateKey key, string locale, string subject, string htmlBody) =>
        new() { Key = key, Locale = locale, Subject = subject, HtmlBody = htmlBody };
}
