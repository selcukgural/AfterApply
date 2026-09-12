namespace AfterApply.Domain.JobSources;

/// <summary>
/// A posting handed to a user by one weekly run. This is the row everything downstream reads —
/// the weekly cap counts it, the 30-day "don't show again" rule reads it, and the scoring and
/// e-mail turns will hang their results off it. Composite key (user, posting): a posting is
/// delivered to a user at most once, ever.
/// </summary>
public sealed class UserJobSourceDelivery
{
    public Guid UserId { get; private set; }

    public Guid PostingId { get; private set; }

    public Guid QueryId { get; private set; }

    public DateTimeOffset DeliveredAt { get; private set; }

    /// <summary>ISO 8601 week the delivery counts against, as <c>yyyyWW</c> (e.g. 202637).</summary>
    public int WeekKey { get; private set; }

    /// <summary>Position in the user's list for that week, 0 first — the order the sweep chose
    /// (best source rank first, round-robin across titles), which is the order the list shows.</summary>
    public int Rank { get; private set; }

    private UserJobSourceDelivery()
    {
    }

    public static UserJobSourceDelivery Create(Guid userId, Guid postingId, Guid queryId, int weekKey, int rank, DateTimeOffset now) =>
        new() { UserId = userId, PostingId = postingId, QueryId = queryId, WeekKey = weekKey, Rank = rank, DeliveredAt = now };
}
