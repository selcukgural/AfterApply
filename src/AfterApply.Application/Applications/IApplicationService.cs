using AfterApply.Application.Applications.Contracts;
using AfterApply.Domain.Applications;

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

    /// <summary>The user-initiated path, behind POST /applications/{id}/status. Always records
    /// StatusChangeOrigin.Manual — a caller cannot claim a different provenance.</summary>
    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken);

    /// <summary>The internal path for changes the system applies on the user's behalf — email
    /// suggestions and imports — where the caller knows the real origin and passes it explicitly.
    /// Not reachable from the HTTP surface.</summary>
    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ApplicationStatus newStatus,
        DateTimeOffset changedAt, StatusChangeContext context, CancellationToken cancellationToken);

    /// <summary>Newest first. Null when the application is not the user's.</summary>
    Task<IReadOnlyCollection<ApplicationStatusHistoryResponse>?> GetStatusHistoryAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ApplicationEventResponse>?> GetTimelineAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationEventResponse?> AddEventAsync(Guid userId, Guid applicationId, CreateEventRequest request, CancellationToken cancellationToken);
}
