using AfterApply.Domain.Applications;
using AfterApply.Domain.Board;
using AfterApply.Domain.Common;

namespace AfterApply.Application.Board.Contracts;

/// <summary>
/// What narrows the board. None of it changes what is on the board — a filter only hides cards
/// from this response. Bound from the query string on both board reads.
/// </summary>
public record BoardFilterQuery
{
    /// <summary>Matched against job title and company name, case-insensitively.</summary>
    public string? Search { get; init; }

    /// <summary>Only applications whose posting came from these sources (the job's own source where
    /// there is one, else the application's). Saved postings are left out while this is set.</summary>
    public Source[]? Sources { get; init; }

    /// <summary>Last activity (any change to the application or saved posting) on or after this.</summary>
    public DateTimeOffset? ActiveFrom { get; init; }

    /// <summary>Last activity on or before this.</summary>
    public DateTimeOffset? ActiveTo { get; init; }

    /// <summary>Only open applications with no activity for Board:SilentDays days.</summary>
    public bool? SilentOnly { get; init; }

    /// <summary>Only applications with a reminder on the dashboard.</summary>
    public bool? WithReminder { get; init; }

    /// <summary>Only open applications the company gave a reply date for.</summary>
    public bool? WithPromise { get; init; }

    /// <summary>Only cards that arrived without the user and have not been looked at yet.</summary>
    public bool? UnseenOnly { get; init; }

    /// <summary>Cards per column on this read; absent, 10 — what the board opens with. Every
    /// member here is nullable because minimal APIs treat a non-nullable bound property as required.</summary>
    public int? Limit { get; init; }
}

/// <summary>One more page of a single column, after <see cref="Cursor"/>.</summary>
public sealed record BoardColumnQuery : BoardFilterQuery
{
    /// <summary>The <see cref="BoardColumnPage.NextCursor"/> of the previous page; absent for the first.</summary>
    public string? Cursor { get; init; }
}

public enum BoardCardKind
{
    Application,
    SavedPosting
}

/// <param name="Id">The card's own id — what move, remove and seen act on.</param>
/// <param name="ItemId">The application's or the saved posting's id, for opening it.</param>
/// <param name="Status">Null for a saved posting.</param>
/// <param name="Source">The site the posting was on (the job's source), else how the application
/// was recorded; null for a saved posting.</param>
/// <param name="LastActivityAt">The last change to the application or posting.</param>
/// <param name="Unseen">True while the arrival mark is shown.</param>
/// <param name="LeavesBoardAt">For a closed application, when it drops off the board.</param>
/// <param name="HasCompanyLogo">Whether GET /api/board/company-logos/{CompanyId} has an image —
/// so the client asks only for logos that exist.</param>
public sealed record BoardCardResponse(
    Guid Id,
    BoardCardKind Kind,
    Guid ItemId,
    string JobTitle,
    Guid CompanyId,
    string CompanyName,
    ApplicationStatus? Status,
    Source? Source,
    DateTimeOffset LastActivityAt,
    BoardCardOrigin Origin,
    bool Unseen,
    DateTimeOffset? LeavesBoardAt,
    bool HasCompanyLogo = false);

/// <param name="Total">Cards in the column that match the filter, across every page.</param>
/// <param name="NextCursor">Null when this page reached the end of the column.</param>
public sealed record BoardColumnPage(
    BoardColumn Column,
    int Total,
    IReadOnlyList<BoardCardResponse> Cards,
    string? NextCursor);

/// <param name="UnseenCount">Cards on the board still carrying the arrival mark, filter or not.</param>
/// <param name="ClosedVisibleDays">How long a closed application stays on the board.</param>
/// <param name="SilentDays">The "no reply for this long" threshold the silence filter and the card tones use.</param>
public sealed record BoardResponse(
    IReadOnlyList<BoardColumnPage> Columns,
    int UnseenCount,
    int ClosedVisibleDays,
    int SilentDays);

/// <summary>Puts applications and saved postings on the board, each on top of its own column.
/// Ids the caller does not own, and ones already on the board, are skipped.</summary>
public sealed record AddBoardCardsRequest(
    IReadOnlyList<Guid>? ApplicationIds,
    IReadOnlyList<Guid>? TrackedJobIds);

/// <param name="Added">How many new cards the request made.</param>
public sealed record AddBoardCardsResponse(int Added);

/// <summary>
/// Moves a card. <see cref="ToStatus"/> set and different from the application's status changes
/// the status (a saved posting becomes an application first); absent, the card is reordered inside
/// its column. The two neighbour ids say where it lands — absent both, on top.
/// </summary>
/// <param name="AboveCardId">The card that will sit directly above it.</param>
/// <param name="BelowCardId">The card that will sit directly below it.</param>
public sealed record MoveBoardCardRequest(
    ApplicationStatus? ToStatus,
    Guid? AboveCardId,
    Guid? BelowCardId);

/// <summary>Clears the arrival mark: from the listed cards, or from every card when
/// <see cref="All"/> is true.</summary>
public sealed record MarkBoardCardsSeenRequest(IReadOnlyList<Guid>? CardIds, bool All);
