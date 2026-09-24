using AfterApply.Domain.Common;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>
/// "Don't ask me about this company" on the dashboard's ended-processes card (contribution loop
/// #10, 2026-09-24). Per company rather than per application because an experience is per
/// company: once a person has said no, a second ended process at the same employer must not ask
/// again. One row per account per company (unique index); the account's deletion takes it along.
/// </summary>
public sealed class ExperienceInviteDismissal : Entity
{
    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public DateTimeOffset DismissedAt { get; private set; }

    private ExperienceInviteDismissal()
    {
    }

    public static ExperienceInviteDismissal Create(Guid userId, Guid companyId, DateTimeOffset now) =>
        new() { UserId = userId, CompanyId = companyId, DismissedAt = now };
}
