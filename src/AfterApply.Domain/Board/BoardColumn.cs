using AfterApply.Domain.Applications;

namespace AfterApply.Domain.Board;

/// <summary>
/// The five columns of the applications board. A card's column is never stored: it is read off the
/// thing the card stands for — a saved posting (TrackedJob) is always <see cref="Saved"/>, an
/// application sits wherever its status puts it. The list and the board can therefore never
/// disagree about where an application is.
/// </summary>
public enum BoardColumn
{
    Saved,
    Applied,
    InProgress,
    Offer,
    Closed
}

public static class BoardColumns
{
    // Concrete HashSets for the same reason as TerminalApplicationStatuses: EF only translates
    // Contains() against a few concrete collection types.
    public static readonly HashSet<ApplicationStatus> AppliedStatuses = [ApplicationStatus.Applied];

    public static readonly HashSet<ApplicationStatus> InProgressStatuses =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview
    ];

    public static readonly HashSet<ApplicationStatus> OfferStatuses = [ApplicationStatus.Offer];

    public static BoardColumn For(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Applied => BoardColumn.Applied,
        ApplicationStatus.Screening or ApplicationStatus.Interview or ApplicationStatus.TechnicalInterview
            or ApplicationStatus.FinalInterview => BoardColumn.InProgress,
        ApplicationStatus.Offer => BoardColumn.Offer,
        _ => BoardColumn.Closed
    };

    /// <summary>The statuses an application in <paramref name="column"/> can have. Empty for
    /// <see cref="BoardColumn.Saved"/>, which holds no applications at all.</summary>
    public static HashSet<ApplicationStatus> StatusesIn(BoardColumn column) => column switch
    {
        BoardColumn.Applied => AppliedStatuses,
        BoardColumn.InProgress => InProgressStatuses,
        BoardColumn.Offer => OfferStatuses,
        BoardColumn.Closed => TerminalApplicationStatuses.Values,
        _ => []
    };
}
