namespace AfterApply.Domain.CandidateExperiences;

/// <summary>
/// How long the whole process took, as a band. Declaration order is the ordinal order — the
/// company page's "typical duration" is the median of these values, so a new band goes where it
/// belongs in time, never at the end.
/// </summary>
public enum ProcessDuration
{
    UnderOneWeek,
    OneToTwoWeeks,
    TwoToFourWeeks,
    OneToTwoMonths,
    OverTwoMonths
}
