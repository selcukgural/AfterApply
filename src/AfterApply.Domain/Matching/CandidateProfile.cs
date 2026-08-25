using AfterApply.Domain.Common;

namespace AfterApply.Domain.Matching;

public sealed class CandidateProfile : AuditableEntity
{
    public Guid UserId { get; private set; }

    public string CvText { get; private set; } = string.Empty;

    private CandidateProfile()
    {
    }

    public static CandidateProfile Create(Guid userId, string cvText, DateTimeOffset now)
    {
        return new CandidateProfile
        {
            UserId = userId,
            CvText = cvText,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateCv(string cvText, DateTimeOffset now)
    {
        CvText = cvText;
        Touch(now);
    }
}
