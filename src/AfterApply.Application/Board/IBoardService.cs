using AfterApply.Application.Board.Contracts;
using AfterApply.Domain.Board;

namespace AfterApply.Application.Board;

/// <summary>
/// The applications board: which of a user's applications and saved postings they keep in front
/// of them, and in what order. Every call is scoped to <c>userId</c>; ids from a request are
/// checked against it, never trusted. The first call for a user fills their board once (open
/// applications with recent activity) — see DECISIONS.md 2026-09-27.
/// </summary>
public interface IBoardService
{
    Task<BoardResponse> GetBoardAsync(Guid userId, BoardFilterQuery query, CancellationToken cancellationToken);

    Task<BoardColumnPage> GetColumnAsync(Guid userId, BoardColumn column, BoardColumnQuery query,
        CancellationToken cancellationToken);

    Task<AddBoardCardsResponse> AddAsync(Guid userId, AddBoardCardsRequest request, CancellationToken cancellationToken);

    /// <summary>Takes a card off the board. The application or saved posting stays. False when the
    /// card is not the caller's.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);

    /// <summary>False when the card is not the caller's.</summary>
    Task<bool> MoveAsync(Guid userId, Guid cardId, MoveBoardCardRequest request, CancellationToken cancellationToken);

    Task MarkSeenAsync(Guid userId, MarkBoardCardsSeenRequest request, CancellationToken cancellationToken);
}

/// <summary>The nightly clean-up: closed applications leave the board after Board:ClosedVisibleDays.</summary>
public interface IBoardMaintenanceService
{
    Task<int> PurgeClosedAsync(CancellationToken cancellationToken);
}
