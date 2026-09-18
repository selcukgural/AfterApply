using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Common;
using AfterApply.Application.CompanySalaries.Contracts;

namespace AfterApply.Application.CompanySalaries;

/// <summary>
/// Every method that names a company or an entry returns null when it does not exist — or, for
/// an entry, when it is not the caller's: "not yours" is indistinguishable from "not there" on
/// the wire, which is the ownership rule the whole API follows.
/// </summary>
public interface ICompanySalaryService
{
    Task<CompanySalaryViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    Task<CompanySalaryPageResponse?> ListForCompanyAsync(Guid companyId, CompanySalaryListQuery query, CancellationToken cancellationToken);

    Task<MyCompanySalaryResponse?> CreateAsync(Guid userId, Guid companyId, CompanySalaryRequest request, CancellationToken cancellationToken);

    Task<MyCompanySalaryResponse?> UpdateAsync(Guid userId, Guid entryId, CompanySalaryRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    Task<MySalariesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The caller's own entries among <paramref name="ids"/>, in no particular order;
    /// anyone else's id is silently absent. Feeds the merged contributions page.</summary>
    Task<IReadOnlyList<MyCompanySalaryResponse>> ListMineByIdsAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<SalaryQuotaResponse> GetQuotaAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Admin only — the caller has already passed <c>IAdminAccessService</c>. Lists every
/// entry with its author and removes one outright; there is no moderation state to move.</summary>
public interface ICompanySalaryAdminService
{
    Task<PagedResult<AdminCompanySalaryListItemResponse>> ListAsync(AdminCompanySalaryListQuery query, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid entryId, CancellationToken cancellationToken);
}

/// <summary>The account holds as many salary entries as it is allowed to. {0} = the limit.</summary>
public sealed class CompanySalaryQuotaReachedException(int limit)
    : CodedException("COMPANY_SALARY_QUOTA_REACHED", $"A user may hold at most {limit} salary entries.", limit);

public sealed class CompanySalaryAlreadyExistsException()
    : CodedException("COMPANY_SALARY_ALREADY_EXISTS", "This account already has a salary entry for this occupation at this company; edit that entry instead.");

/// <summary>The request named an occupation that is not in the catalogue (or is retired).</summary>
public sealed class CompanySalaryOccupationUnknownException()
    : CodedException("COMPANY_SALARY_OCCUPATION_UNKNOWN", "The occupation must be picked from the catalogue.");
