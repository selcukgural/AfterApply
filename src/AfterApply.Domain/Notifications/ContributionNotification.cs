using AfterApply.Domain.Common;

namespace AfterApply.Domain.Notifications;

/// <summary>
/// "Your contribution was found helpful" — one row per contribution per day (DECISIONS.md
/// 2026-09-23, canvas variant C1). Marks on the same contribution on the same Istanbul day fold
/// into this row: <see cref="Count"/> grows and <see cref="LastEventAt"/> moves to the latest one.
/// Who marked it is deliberately not here — not as an id, not as a name. The privacy page says
/// nobody sees who marked what, and the author is the last person that promise should break for.
/// Written with one SQL upsert (ContributionNotificationWriter), never through the change tracker.
/// </summary>
public sealed class ContributionNotification : Entity
{
    public Guid UserId { get; private set; }

    public ContributionNotificationType Type { get; private set; }

    /// <summary>The review, salary entry, experience or blog comment — no foreign key, since it
    /// points at one of four tables; the feed drops a row whose target is gone.</summary>
    public Guid TargetId { get; private set; }

    public DateOnly Day { get; private set; }

    public int Count { get; private set; }

    public DateTimeOffset LastEventAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    private ContributionNotification()
    {
    }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    public void Dismiss(DateTimeOffset now) => DismissedAt ??= now;

    /// <summary>The day a mark belongs to: the site's calendar (Europe/Istanbul), not UTC, so a
    /// mark at 01:30 is grouped with that morning's rather than the previous evening's.</summary>
    public static DateOnly DayOf(DateTimeOffset at) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, SiteTimeZone).DateTime);

    public static readonly TimeZoneInfo SiteTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
}
