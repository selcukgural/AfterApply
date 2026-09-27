namespace AfterApply.Domain.Applications;

/// <summary>The stages an interview date can be recorded in. Screening is one of them: the first
/// call with a recruiter is the interview most candidates have first, and forgetting it costs the
/// same as forgetting a technical round.</summary>
public static class InterviewStages
{
    // Concrete HashSet<T> for the reason TerminalApplicationStatuses gives: EF translates
    // Contains() against it.
    public static readonly HashSet<ApplicationStatus> Values =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview
    ];
}
