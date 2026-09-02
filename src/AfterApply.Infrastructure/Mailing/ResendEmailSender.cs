using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AfterApply.Application.Mailing;
using AfterApply.Domain.Mailing;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Mailing;

/// <summary>Sends password-reset/password-changed email via Resend's REST API
/// (https://resend.com/docs/api-reference/emails/send-email). Subject/HTML come from the
/// EmailTemplates table (see EmailTemplateConfiguration), never compiled into this class — editing
/// a row takes effect on the next send, no redeploy required.
///
/// Always invoked from a Hangfire background job (see AuthService.ForgotPasswordAsync/
/// ResetPasswordAsync), never awaited inline within the triggering HTTP request — so a transient
/// failure here is left to throw, which Hangfire's default automatic-retry filter (10 attempts,
/// exponential backoff) picks up on its own. Only a permanent, retry-proof failure (no API key
/// configured, or Resend rejects the request outright) is swallowed instead of thrown.</summary>
internal sealed class ResendEmailSender(
    HttpClient httpClient, IOptions<ResendOptions> options, AppDbContext dbContext,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    // Matches Program.cs's UseRequestLocalization DefaultRequestCulture — used when the requested
    // locale has no row of its own.
    private const string FallbackLocale = "tr";

    private readonly ResendOptions _options = options.Value;

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string locale, CancellationToken cancellationToken)
    {
        var template = await GetTemplateAsync(EmailTemplateKey.PasswordReset, locale, cancellationToken);
        var html = template.HtmlBody.Replace("{{ResetLink}}", resetLink);
        await SendAsync(toEmail, template.Subject, html, cancellationToken);
    }

    public async Task SendPasswordChangedEmailAsync(string toEmail, string locale, CancellationToken cancellationToken)
    {
        var template = await GetTemplateAsync(EmailTemplateKey.PasswordChanged, locale, cancellationToken);
        await SendAsync(toEmail, template.Subject, template.HtmlBody, cancellationToken);
    }

    private async Task<EmailTemplate> GetTemplateAsync(EmailTemplateKey key, string locale, CancellationToken cancellationToken)
    {
        var template = await dbContext.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == key && t.Locale == locale, cancellationToken);

        if (template is not null)
        {
            return template;
        }

        // No row for this specific locale — fall back rather than drop the send entirely. A
        // missing FallbackLocale row too is a real misconfiguration, worth throwing for (Hangfire
        // retries it and surfaces it in the dashboard) rather than silently swallowing.
        return await dbContext.EmailTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == key && t.Locale == FallbackLocale, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No EmailTemplates row for key '{key}', locale '{locale}' or fallback '{FallbackLocale}'.");
    }

    private async Task SendAsync(string toEmail, string subject, string html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // Permanent until someone sets the key — retrying changes nothing.
            logger.LogWarning("Resend:ApiKey is not configured; skipping outbound email.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = $"{_options.FromName} <{_options.FromEmail}>",
            to = new[] { toEmail },
            subject,
            html
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // 429/5xx are exactly what Hangfire's retry exists for (rate limiting, Resend having a
        // bad moment); a 4xx here means the request itself is wrong (bad payload, revoked key) and
        // will fail identically on every retry, so it's logged and dropped instead of retried.
        var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
        if (isTransient)
        {
            throw new HttpRequestException($"Resend email send failed with transient status {(int)response.StatusCode}.");
        }

        logger.LogWarning("Resend email send failed with non-retryable status {StatusCode}.", (int)response.StatusCode);
    }
}
