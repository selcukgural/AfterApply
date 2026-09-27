using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Board;

/// <summary>What the board does when something outside it changes an application.</summary>
public enum BoardPlacementAction
{
    /// <summary>Leave the board as it is. A card already on it still shows the new column, since
    /// the column is read off the status; it just keeps its place.</summary>
    None,

    /// <summary>Put the card on top of its (new) column, no mark — the user did it.</summary>
    MoveToTop,

    /// <summary>Put the card on top of its column and mark it as having come from e-mail.</summary>
    MoveToTopFromEmail,

    /// <summary>The card was not on the board: bring it back on top, marked as returned.</summary>
    ReturnFromEmail
}

/// <summary>
/// The rules for how outside events move cards (DECISIONS.md 2026-09-27, "Başvuru panosu"), in one
/// place so they can be read — and tested — as a table.
/// </summary>
public static class BoardAutoPlacement
{
    /// <summary>The statuses an e-mail may bring a card back to the board with. Progress only: the
    /// user took the card off, and a rejection or silence is not a reason to put it in front of
    /// them again.</summary>
    private static readonly HashSet<ApplicationStatus> ProgressStatuses =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview, ApplicationStatus.Offer
    ];

    public static BoardPlacementAction ForStatusChange(bool onBoard, ApplicationStatus newStatus, StatusChangeOrigin origin)
    {
        var fromEmail = origin is StatusChangeOrigin.EmailAutoApplied or StatusChangeOrigin.EmailSuggestionConfirmed;

        if (!onBoard)
        {
            return fromEmail && ProgressStatuses.Contains(newStatus)
                ? BoardPlacementAction.ReturnFromEmail
                : BoardPlacementAction.None;
        }

        if (fromEmail)
        {
            return BoardPlacementAction.MoveToTopFromEmail;
        }

        // A change the user made (one card or a bulk selection) lifts the card to the top of where
        // it went. Reverts, imports and system changes put a status back or fill history in; they
        // leave the card where the user last had it.
        return origin is StatusChangeOrigin.Manual or StatusChangeOrigin.BulkEdit
            ? BoardPlacementAction.MoveToTop
            : BoardPlacementAction.None;
    }

    /// <summary>The origin a newly created application gets its card with, or null when creating
    /// it does not put it on the board (imports: a thousand old rows are the list's business).</summary>
    public static BoardCardOrigin? ForNewApplication(Source source) => source switch
    {
        Source.CsvImport or Source.LinkedInImport => null,
        Source.BrowserExtension => BoardCardOrigin.Extension,
        Source.Email => BoardCardOrigin.Email,
        _ => BoardCardOrigin.Manual
    };
}
