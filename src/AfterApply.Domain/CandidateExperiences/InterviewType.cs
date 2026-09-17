namespace AfterApply.Domain.CandidateExperiences;

/// <summary>The kinds of step a process can include; a candidate ticks any number of them.
/// Stored one row per tick in <c>CandidateExperienceInterviewTypes</c>.</summary>
public enum InterviewType
{
    Phone,
    Video,
    OnSite,
    TechnicalTest,
    TakeHomeAssignment,
    Panel,
    AssessmentCenter
}
