using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.Notifications;

/// <summary>
/// Turns a new helpful mark into a notification for the contribution's author (DECISIONS.md
/// 2026-09-23). Called by the four ToggleHelpfulAsync methods after their own mark is saved, and
/// only on the "marked" branch.
/// </summary>
internal sealed class ContributionNotificationWriter(
    AppDbContext dbContext,
    ILogger<ContributionNotificationWriter> logger,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// One statement: record the reader in the first-mark ledger, and only if that insert went in —
    /// the first time this reader marked this contribution — upsert the author's row for the day.
    /// A reader toggling on/off/on is counted once; two readers racing both land, since each owns
    /// a different ledger key and the day row's conflict clause adds rather than overwrites. A new
    /// mark on a row the author already read or cleared brings it back as unread.
    /// Best effort: the mark is already saved and is what the reader asked for, so a failure here
    /// is logged, not thrown.
    /// </summary>
    public async Task RecordHelpfulAsync(ContributionNotificationType type, Guid targetId, Guid authorUserId, Guid voterUserId,
        CancellationToken cancellationToken)
    {
        // Blog comments let an author vote on their own comment; the other three refuse it upstream.
        if (authorUserId == voterUserId)
        {
            return;
        }

        try
        {
            var notify = await ShouldNotifyAsync(type, authorUserId, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            var typeName = type.ToString();

            if (!notify)
            {
                // Still counted, so switching the setting back on does not turn this reader's
                // next re-toggle into a notification about an old mark.
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "HelpfulNotificationLedger" ("Id", "Type", "TargetId", "VoterUserId", "CountedAt")
                    VALUES ({Guid.CreateVersion7()}, {typeName}, {targetId}, {voterUserId}, {now})
                    ON CONFLICT ("Type", "TargetId", "VoterUserId") DO NOTHING
                    """, cancellationToken);
                return;
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH counted AS (
                    INSERT INTO "HelpfulNotificationLedger" ("Id", "Type", "TargetId", "VoterUserId", "CountedAt")
                    VALUES ({Guid.CreateVersion7()}, {typeName}, {targetId}, {voterUserId}, {now})
                    ON CONFLICT ("Type", "TargetId", "VoterUserId") DO NOTHING
                    RETURNING 1
                )
                INSERT INTO "ContributionNotifications"
                    ("Id", "UserId", "Type", "TargetId", "Day", "Count", "LastEventAt", "ReadAt", "DismissedAt")
                SELECT {Guid.CreateVersion7()}, {authorUserId}, {typeName}, {targetId}, {ContributionNotification.DayOf(now)}, 1, {now}, NULL, NULL
                FROM counted
                ON CONFLICT ("UserId", "Type", "TargetId", "Day") DO UPDATE SET
                    "Count" = "ContributionNotifications"."Count" + 1,
                    "LastEventAt" = EXCLUDED."LastEventAt",
                    "ReadAt" = NULL,
                    "DismissedAt" = NULL
                """, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not record a {Type} notification for target {TargetId}", type, targetId);
        }
    }

    private async Task<bool> ShouldNotifyAsync(ContributionNotificationType type, Guid authorUserId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Users
            .Where(u => u.Id == authorUserId)
            .Select(u => new
            {
                u.NotifyContributions, u.NotifyReviewHelpful, u.NotifySalaryHelpful,
                u.NotifyExperienceHelpful, u.NotifyBlogCommentHelpful
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return false;
        }

        return NotificationPreferenceRules.Allows(type, settings.NotifyContributions, settings.NotifyReviewHelpful,
            settings.NotifySalaryHelpful, settings.NotifyExperienceHelpful, settings.NotifyBlogCommentHelpful);
    }
}
