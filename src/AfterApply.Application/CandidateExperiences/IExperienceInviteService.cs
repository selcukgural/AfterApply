using AfterApply.Application.CandidateExperiences.Contracts;

namespace AfterApply.Application.CandidateExperiences;

/// <summary>
/// The dashboard's "ended processes" card (contribution loop #10, 2026-09-24): applications that
/// closed — rejected, gone quiet, or an accepted offer — long enough ago, at companies the caller
/// has not rated yet and has not waved away. Computed on each read rather than stored like the
/// reminders are: the reminder machinery exists to follow live applications and drops every
/// closed one on purpose, which is exactly the set this lists.
/// </summary>
public interface IExperienceInviteService
{
    Task<IReadOnlyList<ExperienceInviteResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stop asking about this company. Idempotent. False when there is no such company
    /// among the caller's own applications — someone else's company id is "not found".</summary>
    Task<bool> DismissAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
}
