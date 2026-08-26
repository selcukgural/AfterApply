namespace AfterApply.Domain.Applications;

/// <summary>Statuses where the company itself gave the candidate an explicit outcome (Rejected,
/// Accepted). Deliberately narrower than <see cref="TerminalApplicationStatuses"/>: Ghosted is
/// excluded because it is the absence of closure, not an instance of it, and Withdrawn is
/// excluded because it is the candidate's own decision, not a signal about the company. Used by
/// the Candidate Experience Score's Closure Rate sub-metric.</summary>
public static class CompanyGivenClosureStatuses
{
    // Concrete HashSet<T>, not IReadOnlySet<T> — see TerminalApplicationStatuses.cs: EF Core's
    // query translator only recognizes .Contains() calls against a few concrete collection types
    // when building SQL IN/ANY clauses.
    public static readonly HashSet<ApplicationStatus> Values =
    [
        ApplicationStatus.Rejected, ApplicationStatus.Accepted
    ];
}
