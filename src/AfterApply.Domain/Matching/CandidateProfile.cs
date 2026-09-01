using AfterApply.Domain.Common;

namespace AfterApply.Domain.Matching;

public sealed class CandidateProfile : AuditableEntity
{
    public Guid UserId { get; private set; }

    public string CvText { get; private set; } = string.Empty;

    // Stamped by Create/UpdateCv, both of which are only ever called after the request validator
    // has already required explicit consent (see UpdateCandidateProfileRequestValidator) — so
    // reaching either method means consent was just given. Re-stamped on every CV edit rather
    // than set once, so consent stays tied to the CV text actually on file.
    public DateTimeOffset? OpenAiConsentAcceptedAt { get; private set; }

    private CandidateProfile()
    {
    }

    public static CandidateProfile Create(Guid userId, string cvText, DateTimeOffset now)
    {
        return new CandidateProfile
        {
            UserId = userId,
            CvText = cvText,
            OpenAiConsentAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateCv(string cvText, DateTimeOffset now)
    {
        CvText = cvText;
        OpenAiConsentAcceptedAt = now;
        Touch(now);
    }
}
