using AfterApply.Domain.Common;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>One interview type the candidate ticked. A row per tick rather than an array column so
/// the company page can count them in the database, the same way it counts statement picks.</summary>
public sealed class CandidateExperienceInterviewType : Entity
{
    public Guid ExperienceId { get; private set; }

    public InterviewType Type { get; private set; }

    private CandidateExperienceInterviewType()
    {
    }

    public static CandidateExperienceInterviewType Create(Guid experienceId, InterviewType type) =>
        new() { ExperienceId = experienceId, Type = type };
}
