namespace AfterApply.Domain.CandidateExperiences;

/// <summary>How the process ended for this candidate, as they report it. <see cref="NoResponse"/>
/// is the candidate saying "I was never told" — a statement about their own inbox, which is what
/// the company page counts as "candidates never told the outcome".</summary>
public enum HiringOutcome
{
    Offer,
    Rejected,
    InProgress,
    Withdrew,
    NoResponse
}
