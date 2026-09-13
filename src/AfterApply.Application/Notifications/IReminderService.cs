using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Application.Notifications;

public interface IReminderService
{
    Task<IReadOnlyList<ReminderResponse>> GetActiveRemindersAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    /// <summary>
    /// The user answered a follow-up reminder by actually following up: records a FollowUpSent
    /// event on the application and closes the reminder in one step. False when the reminder is
    /// not this user's or is already closed.
    /// </summary>
    Task<bool> MarkFollowedUpAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    /// <summary>
    /// Scans applications across all users and persists new Reminder rows for
    /// applications that have crossed a follow-up or ghosting threshold, and retires
    /// reminders that no longer apply (the application reached a terminal status,
    /// went past the stale horizon, or a stronger "possibly ghosted" superseded its
    /// follow-up). Invoked by the Hangfire recurring job; has no per-request user
    /// context. Returns the number of reminders created.
    /// </summary>
    Task<int> ScanAndGenerateRemindersAsync(CancellationToken cancellationToken);
}
