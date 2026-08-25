namespace AfterApply.Domain.Applications;

public static class ApplicationStatusClassification
{
    // Concrete HashSet<T>, not IReadOnlySet<T> — see TerminalApplicationStatuses.cs: EF Core's
    // query translator only recognizes .Contains() calls against a few concrete collection types
    // when building SQL IN/ANY clauses.
    public static readonly HashSet<ApplicationStatus> RespondedStatuses =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview, ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Accepted
    ];

    public static readonly HashSet<ApplicationStatus> InterviewStatuses =
    [
        ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview, ApplicationStatus.FinalInterview
    ];

    public static readonly HashSet<ApplicationStatus> OfferStatuses =
    [
        ApplicationStatus.Offer, ApplicationStatus.Accepted
    ];
}
