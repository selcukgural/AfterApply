using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Application.Notifications;

public interface IReminderService
{
    Task<IReadOnlyList<ReminderResponse>> GetActiveRemindersAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    /// <summary>
    /// Scans applications across all users and persists new Reminder rows for
    /// applications that have crossed a follow-up or ghosting threshold. Invoked
    /// by the Hangfire recurring job; has no per-request user context.
    /// </summary>
    Task<int> ScanAndGenerateRemindersAsync(CancellationToken cancellationToken);
}
