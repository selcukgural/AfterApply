using AfterApply.Application.Applications.Contracts;

namespace AfterApply.Application.Applications;

public interface IApplicationService
{
    Task<PagedResult<ApplicationSummaryResponse>> GetAllAsync(Guid userId, GetApplicationsQuery query, CancellationToken cancellationToken);

    Task<ApplicationSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> GetByIdAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse> CreateAsync(Guid userId, CreateApplicationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the browser extension's "I Applied" action (spec §11/Sprint 9). Unlike CreateAsync
    /// (a deliberate one-off manual entry, never deduplicated), this resolves the Job via
    /// IJobResolver and, when an application with the same JobUrl already exists for this user,
    /// returns it instead of creating a duplicate (WasDuplicate = true) — the extension button can
    /// be clicked more than once on the same job page without piling up rows.
    /// </summary>
    Task<ExtensionApplicationResponse> CreateFromExtensionAsync(Guid userId, CreateFromExtensionRequest request, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> UpdateAsync(Guid userId, Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ApplicationEventResponse>?> GetTimelineAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationEventResponse?> AddEventAsync(Guid userId, Guid applicationId, CreateEventRequest request, CancellationToken cancellationToken);
}
