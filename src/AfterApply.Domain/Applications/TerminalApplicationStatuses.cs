namespace AfterApply.Domain.Applications;

/// <summary>Statuses that mean an application is no longer actively in play (withdrawn by the
/// candidate, ghosted, rejected, or accepted). Used wherever a background scan needs to skip
/// applications that no longer need attention.</summary>
public static class TerminalApplicationStatuses
{
    // Concrete HashSet<T>, not IReadOnlySet<T> — EF Core's query translator only
    // recognizes .Contains() calls against a few concrete collection types when
    // building SQL IN/ANY clauses; the interface type failed to translate (verified).
    public static readonly HashSet<ApplicationStatus> Values =
    [
        ApplicationStatus.Withdrawn, ApplicationStatus.Ghosted, ApplicationStatus.Rejected, ApplicationStatus.Accepted
    ];
}
