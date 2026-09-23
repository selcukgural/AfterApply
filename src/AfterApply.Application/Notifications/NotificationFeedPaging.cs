using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Application.Notifications;

/// <summary>
/// One page of two newest-first lists merged as if they were one. Each list must hold at least
/// its first <c>page * pageSize</c> rows — however many of either can land on the pages up to
/// this one — which is why the feed reads that many from each source and caps the page number.
/// Ties on time keep the id order stable, so a row cannot swap sides of a page boundary.
/// </summary>
public static class NotificationFeedPaging
{
    public static IReadOnlyList<NotificationFeedItemResponse> Page(
        IEnumerable<NotificationFeedItemResponse> first,
        IEnumerable<NotificationFeedItemResponse> second,
        int page,
        int pageSize) =>
        first.Concat(second)
            .OrderByDescending(i => i.OccurredAt)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
}
