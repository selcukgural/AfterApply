using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Application.Notifications;

/// <summary>
/// The bell: contribution notifications and the Gmail scan's status changes as one list, newest
/// first. Every method is scoped to <c>userId</c>; an id that is not the caller's is "not found".
/// Gmail rows are left out entirely — listed, counted, marked, cleared — while the account has
/// them switched off or the Gmail feature itself is off.
/// </summary>
public interface INotificationFeedService
{
    Task<PagedResult<NotificationFeedItemResponse>> ListAsync(Guid userId, GetNotificationFeedQuery query, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task DismissAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<NotificationPreferencesResponse> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Deletes contribution notifications the user has read or cleared, and the first-mark
/// ledger, once they are older than <c>Notifications:ContributionRetentionDays</c>.</summary>
public interface IContributionNotificationRetentionService
{
    Task<int> PurgeAsync(CancellationToken cancellationToken);
}
