using AfterApply.Domain.Common;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>One optional category rating of an experience. Absent row = the candidate left that
/// category blank, which is different from any number and is why these are not columns.</summary>
public sealed class CandidateExperienceCategoryRating : Entity
{
    public Guid ExperienceId { get; private set; }

    public ExperienceCategory Category { get; private set; }

    public int Rating { get; private set; }

    private CandidateExperienceCategoryRating()
    {
    }

    public static CandidateExperienceCategoryRating Create(Guid experienceId, ExperienceCategory category, int rating) =>
        new() { ExperienceId = experienceId, Category = category, Rating = rating };
}
