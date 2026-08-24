using AfterApply.Application.Applications.Contracts;

namespace AfterApply.Application.Applications;

public interface IApplicationService
{
    Task<IReadOnlyCollection<ApplicationSummaryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> GetByIdAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse> CreateAsync(Guid userId, CreateApplicationRequest request, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> UpdateAsync(Guid userId, Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ApplicationEventResponse>?> GetTimelineAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationEventResponse?> AddEventAsync(Guid userId, Guid applicationId, CreateEventRequest request, CancellationToken cancellationToken);
}
