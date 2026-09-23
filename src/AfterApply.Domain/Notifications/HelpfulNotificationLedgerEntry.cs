using AfterApply.Domain.Common;

namespace AfterApply.Domain.Notifications;

/// <summary>
/// "This reader has already been counted for this contribution." Helpful marks are toggles and an
/// un-mark deletes the mark row, so without this a reader clicking on/off/on would tell the author
/// three times. Only the first mark per (type, target, reader) ever reaches a notification. Kept
/// apart from <see cref="ContributionNotification"/> so that row never learns who the reader was;
/// removed with the reader's account and by the notification purge.
/// </summary>
public sealed class HelpfulNotificationLedgerEntry : Entity
{
    public ContributionNotificationType Type { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid VoterUserId { get; private set; }

    public DateTimeOffset CountedAt { get; private set; }

    private HelpfulNotificationLedgerEntry()
    {
    }
}
