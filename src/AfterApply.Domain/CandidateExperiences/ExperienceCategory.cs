namespace AfterApply.Domain.CandidateExperiences;

/// <summary>
/// The nine things a candidate can rate about a hiring process. <see cref="Overall"/> is the one
/// required rating and is stored on the experience row itself (<c>OverallRating</c>, which the
/// Bayesian company score reads); the other eight are optional and live in
/// <c>CandidateExperienceCategoryRatings</c>. Order here is the order the form and the summary
/// panel show them in.
/// </summary>
public enum ExperienceCategory
{
    Overall,
    Communication,
    ResponseTime,
    Punctuality,
    InterviewerPreparation,
    QuestionRelevance,
    Transparency,
    AssignmentLoad,
    OutcomeCommunication
}
