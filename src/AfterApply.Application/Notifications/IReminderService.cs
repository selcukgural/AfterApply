using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Application.Notifications;

public interface IReminderService
{
    /// <summary>
    /// One page of the user's active reminders, longest-waiting first. Paged since 2026-09-13: the
    /// dashboard card shows five rows, and a list that once carried 1,224 of them in one response
    /// must not be able to again.
    /// </summary>
    Task<PagedResult<ReminderResponse>> GetActiveRemindersAsync(Guid userId, GetRemindersQuery query, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    /// <summary>
    /// The user answered a follow-up reminder by actually following up: records a FollowUpSent
    /// event on the application and closes the reminder in one step. False when the reminder is
    /// not this user's or is already closed.
    /// </summary>
    Task<bool> MarkFollowedUpAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    /// <summary>"Dismiss" for many reminders at once. Scoped to the caller: a foreign or already
    /// closed id counts for nothing rather than failing the batch.</summary>
    Task<BulkReminderResponse> BulkDismissAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken);

    /// <summary><see cref="MarkFollowedUpAsync"/> for many reminders at once: one FollowUpSent event
    /// per application, every selected reminder closed.</summary>
    Task<BulkReminderResponse> BulkMarkFollowedUpAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// "It was ghosted" for many reminders at once: the applications behind the selection move to
    /// Ghosted through the application service, which closes their reminders as it does for any
    /// terminal status. The response is a bulk status change's, so the caller gets the same undo
    /// as everywhere else; the set is resolved here from the caller's own rows and is not subject to
    /// the applications bulk ceiling.
    /// </summary>
    Task<BulkChangeStatusResponse> BulkMarkGhostedAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken);

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
