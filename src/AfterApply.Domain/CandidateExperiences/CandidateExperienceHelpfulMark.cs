using AfterApply.Domain.Common;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>A reader's "helpful" on a candidate experience — the <c>CompanyReviewHelpfulMark</c>
/// shape. One per reader per experience (unique index); toggling off deletes the row.</summary>
public sealed class CandidateExperienceHelpfulMark : Entity
{
    public Guid ExperienceId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset MarkedAt { get; private set; }

    private CandidateExperienceHelpfulMark()
    {
    }

    public static CandidateExperienceHelpfulMark Create(Guid experienceId, Guid userId, DateTimeOffset now) =>
        new() { ExperienceId = experienceId, UserId = userId, MarkedAt = now };
}
