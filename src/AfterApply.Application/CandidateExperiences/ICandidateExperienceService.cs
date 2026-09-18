using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Common;

namespace AfterApply.Application.CandidateExperiences;

/// <summary>
/// Every method that names a company or an experience returns null when it does not exist — or,
/// for an experience, when it is not the caller's: "not yours" is indistinguishable from "not
/// there" on the wire, which is the ownership rule the whole API follows.
/// </summary>
public interface ICandidateExperienceService
{
    Task<CandidateExperienceViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    /// <summary>The public page, by slug because it is read from the public company page.</summary>
    Task<CandidateExperiencePageResponse?> ListForCompanyAsync(string slug, CandidateExperienceListQuery query, CancellationToken cancellationToken);

    Task<MyCandidateExperienceResponse?> CreateAsync(Guid userId, Guid companyId, CandidateExperienceRequest request, CancellationToken cancellationToken);

    Task<MyCandidateExperienceResponse?> UpdateAsync(Guid userId, Guid experienceId, CandidateExperienceRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid experienceId, CancellationToken cancellationToken);

    Task<MyCandidateExperiencesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The caller's own entries among <paramref name="ids"/>, in no particular order;
    /// anyone else's id is silently absent. Feeds the merged contributions page.</summary>
    Task<IReadOnlyList<MyCandidateExperienceResponse>> ListMineByIdsAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<ExperienceQuotaResponse> GetQuotaAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Admin only — the caller has already passed <c>IAdminAccessService</c>. Lists every
/// entry with its author and removes one outright; there is no moderation state to move.</summary>
public interface ICandidateExperienceAdminService
{
    Task<PagedResult<AdminCandidateExperienceListItemResponse>> ListAsync(AdminCandidateExperienceListQuery query, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid experienceId, CancellationToken cancellationToken);
}

/// <summary>The account holds as many experiences as it is allowed to. {0} = the limit.</summary>
public sealed class CandidateExperienceQuotaReachedException(int limit)
    : CodedException("CANDIDATE_EXPERIENCE_QUOTA_REACHED", $"A user may hold at most {limit} candidate experiences.", limit);

public sealed class CandidateExperienceAlreadyExistsException()
    : CodedException("CANDIDATE_EXPERIENCE_ALREADY_EXISTS", "This account already rated its hiring process at this company; edit that entry instead.");
