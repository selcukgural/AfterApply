using AfterApply.Domain.Common;

namespace AfterApply.Domain.Notifications;

public sealed class Reminder : Entity
{
    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public ReminderType Type { get; private set; }

    public DateTimeOffset ReferenceAt { get; private set; }

    public int DaysElapsedAtCreation { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>"Not now": the reminder stays open but is off the list until this moment. Chosen
    /// from a few fixed lengths on the row, never a date — see SnoozeReminderRequest.</summary>
    public DateTimeOffset? SnoozedUntil { get; private set; }

    /// <summary>Set when the scan held the reminder back from a weekend or public holiday to the
    /// next working day (SnoozedUntil is then that morning), so the row can say why it is showing
    /// today. Cleared the moment the user snoozes or reopens it themselves.</summary>
    public ReminderDeferral? DeferredFor { get; private set; }

    private Reminder()
    {
    }

    public static Reminder Create(Guid userId, Guid applicationId, ReminderType type,
        DateTimeOffset referenceAt, int daysElapsedAtCreation, DateTimeOffset now)
    {
        return new Reminder
        {
            UserId = userId,
            ApplicationId = applicationId,
            Type = type,
            ReferenceAt = referenceAt,
            DaysElapsedAtCreation = daysElapsedAtCreation,
            CreatedAt = now
        };
    }

    public void Dismiss(DateTimeOffset now)
    {
        DismissedAt = now;
    }

    public void Snooze(DateTimeOffset until)
    {
        SnoozedUntil = until;
        DeferredFor = null;
    }

    /// <summary>Held back by the scan to a working day — not the user's snooze.</summary>
    public void Defer(DateTimeOffset until, ReminderDeferral reason)
    {
        SnoozedUntil = until;
        DeferredFor = reason;
    }

    /// <summary>The undo of both a snooze and an answer that closed the row: back on the list as it
    /// was.</summary>
    public void Reopen()
    {
        DismissedAt = null;
        SnoozedUntil = null;
        DeferredFor = null;
    }
}
