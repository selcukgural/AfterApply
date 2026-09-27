using AfterApply.Domain.Common;

namespace AfterApply.Domain.Board;

/// <summary>How a card got onto the board. Everything but <see cref="Seed"/> and
/// <see cref="Manual"/> arrived without the user doing it on the board, and is shown with a mark
/// until they have looked at it.</summary>
public enum BoardCardOrigin
{
    /// <summary>Put there by the first opening of the board (recent, still open applications).</summary>
    Seed,

    /// <summary>The user added it, or created the application or saved posting by hand.</summary>
    Manual,

    /// <summary>An e-mail created it or moved it while it was on the board.</summary>
    Email,

    /// <summary>The user had taken it off the board; an e-mail about progress brought it back.</summary>
    EmailReturned,

    /// <summary>"I applied" in the browser extension.</summary>
    Extension,

    /// <summary>"Apply later" in the browser extension.</summary>
    Later
}

/// <summary>
/// One application or one saved posting on a user's board, and where in its column it sits.
/// Exactly one of <see cref="ApplicationId"/> and <see cref="TrackedJobId"/> is set (a check
/// constraint holds that). Taking a card off the board deletes this row and nothing else: the
/// application stays in the list.
/// </summary>
public sealed class BoardCard : Entity
{
    public Guid UserId { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public Guid? TrackedJobId { get; private set; }

    /// <summary>Sort key inside the column, ascending from the top. Sparse (see
    /// <see cref="BoardPositions"/>) so a move rewrites one row, not the column.</summary>
    public long Position { get; private set; }

    public BoardCardOrigin Origin { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    /// <summary>Null while the arrival mark is shown — the card came in without the user.</summary>
    public DateTimeOffset? SeenAt { get; private set; }

    /// <summary>When the board saw the application close (move to a terminal status). A closed
    /// card stays visible for a fixed number of days from here, then leaves the board. The time
    /// the board learned it, not the status change's own date — a rejection logged today with
    /// last month's date is still news today.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    private BoardCard()
    {
    }

    public static BoardCard ForApplication(Guid userId, Guid applicationId, bool isClosed, long position,
        BoardCardOrigin origin, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            ApplicationId = applicationId,
            Position = position,
            Origin = origin,
            AddedAt = now,
            SeenAt = IsQuiet(origin) ? now : null,
            ClosedAt = isClosed ? now : null
        };

    public static BoardCard ForTrackedJob(Guid userId, Guid trackedJobId, long position, BoardCardOrigin origin,
        DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            TrackedJobId = trackedJobId,
            Position = position,
            Origin = origin,
            AddedAt = now,
            SeenAt = IsQuiet(origin) ? now : null
        };

    public void MoveTo(long position) => Position = position;

    /// <summary>Records that something outside the board changed this card, which shows the mark
    /// again until the user looks.</summary>
    public void MarkArrived(BoardCardOrigin origin)
    {
        Origin = origin;
        SeenAt = IsQuiet(origin) ? SeenAt : null;
    }

    public void MarkSeen(DateTimeOffset now) => SeenAt ??= now;

    /// <summary>Keeps <see cref="ClosedAt"/> in step with the application's status: set on the
    /// first move into a terminal status, cleared when it reopens.</summary>
    public void SyncClosed(bool isClosed, DateTimeOffset now)
    {
        if (!isClosed)
        {
            ClosedAt = null;
            return;
        }

        ClosedAt ??= now;
    }

    /// <summary>A saved posting became an application: the card follows it rather than being
    /// dropped and re-added, so the user's own placement and the card's history survive.</summary>
    public void BecomeApplication(Guid applicationId, long position)
    {
        ApplicationId = applicationId;
        TrackedJobId = null;
        Position = position;
    }

    private static bool IsQuiet(BoardCardOrigin origin) => origin is BoardCardOrigin.Seed or BoardCardOrigin.Manual;
}

/// <summary>Marks that a user's board exists: its first opening filled it (see
/// <see cref="BoardCardOrigin.Seed"/>). Until then no event adds cards — the seed will pick the
/// same applications up anyway, and a user who never opens the board costs nothing.</summary>
public sealed class BoardState
{
    public Guid UserId { get; private set; }

    public DateTimeOffset SeededAt { get; private set; }

    private BoardState()
    {
    }

    public static BoardState Seeded(Guid userId, DateTimeOffset now) => new() { UserId = userId, SeededAt = now };
}
