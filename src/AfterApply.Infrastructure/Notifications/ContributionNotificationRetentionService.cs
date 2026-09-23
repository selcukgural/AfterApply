using AfterApply.Application.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Notifications;

public sealed class ContributionNotificationRetentionService(
    AppDbContext dbContext,
    IOptions<NotificationOptions> options,
    ILogger<ContributionNotificationRetentionService> logger) : IContributionNotificationRetentionService
{
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.ContributionRetentionDays);

        // An unread row stays however old it is: the author has not seen it yet.
        var notifications = await dbContext.ContributionNotifications
            .Where(n => n.LastEventAt < cutoff && (n.ReadAt != null || n.DismissedAt != null))
            .ExecuteDeleteAsync(cancellationToken);

        // Past the cutoff a reader's re-mark may notify again — a mark three months later is news.
        var ledger = await dbContext.HelpfulNotificationLedger
            .Where(e => e.CountedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (notifications + ledger > 0)
        {
            logger.LogInformation("Purged {Notifications} contribution notifications and {Ledger} ledger rows older than {Cutoff}",
                notifications, ledger, cutoff);
        }

        return notifications + ledger;
    }
}
