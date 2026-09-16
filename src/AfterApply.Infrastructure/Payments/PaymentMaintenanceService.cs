using AfterApply.Application.Mailing;
using AfterApply.Application.Payments;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

internal sealed class PaymentMaintenanceService(
    AppDbContext dbContext,
    IOptions<PayTrOptions> options,
    IOptions<AppOptions> appOptions,
    IBackgroundJobClient jobClient,
    ILogger<PaymentMaintenanceService> logger,
    TimeProvider? timeProvider = null) : IPaymentMaintenanceService
{
    private readonly PayTrOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> ExpirePendingOrdersAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var grace = TimeSpan.FromMinutes(_options.PendingGraceMinutes);
        // Orders that never got a token (PayTR call crashed mid-way) have no expiry stamp; they
        // are closed on the same schedule from their creation time.
        var cutoffWithToken = now - grace;
        var cutoffWithoutToken = now - _options.TimeoutLimit - grace;

        var stale = await dbContext.PaymentOrders
            .Where(o => o.Status == PaymentOrderStatus.Pending &&
                        ((o.TokenExpiresAt != null && o.TokenExpiresAt < cutoffWithToken) ||
                         (o.TokenExpiresAt == null && o.CreatedAt < cutoffWithoutToken)))
            .ToListAsync(cancellationToken);

        foreach (var order in stale)
        {
            order.MarkExpired(now);
        }

        if (stale.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Expired {Count} pending PayTR orders", stale.Count);
        }

        return stale.Count;
    }

    public async Task<int> SendExpiryRemindersAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromDays(Math.Max(_options.ExpiryReminderDays, 1));
        var horizon = now + window;

        var due = await dbContext.ProEntitlements
            .Where(e => e.RevokedAt == null && e.ActiveUntil > now && e.ActiveUntil <= horizon && e.ExpiryReminderSentFor != e.ActiveUntil)
            .Join(dbContext.Users, e => e.UserId, u => u.Id, (e, u) => new { Entitlement = e, u.Email, u.PreferredLanguage })
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in due)
        {
            if (string.IsNullOrWhiteSpace(row.Email))
            {
                continue;
            }

            var locale = PaymentFormatting.NormalizeLocale(row.PreferredLanguage);
            var activeUntilText = PaymentFormatting.Date(row.Entitlement.ActiveUntil, locale);
            var renewLink = $"{appOptions.Value.WebBaseUrl.TrimEnd('/')}/{locale}/pro";
            var email = row.Email;

            // Marked before the e-mail is queued: a reminder that is lost is a smaller failure
            // than one sent every day until the period ends.
            row.Entitlement.MarkExpiryReminderSent();
            await dbContext.SaveChangesAsync(cancellationToken);
            jobClient.Enqueue<IEmailSender>(s => s.SendProExpiringEmailAsync(email, locale, activeUntilText, renewLink, CancellationToken.None));
            sent++;
        }

        if (sent > 0)
        {
            logger.LogInformation("Queued {Count} Pro expiry reminders", sent);
        }

        return sent;
    }
}
