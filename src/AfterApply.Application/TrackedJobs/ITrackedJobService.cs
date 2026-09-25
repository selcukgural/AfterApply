using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.TrackedJobs.Contracts;

namespace AfterApply.Application.TrackedJobs;

public interface ITrackedJobService
{
    Task<IReadOnlyCollection<TrackedJobResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<TrackedJobResponse> CreateAsync(Guid userId, CreateTrackedJobRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The browser extension's "Apply later": the same capture "I Applied" submits, saved as a
    /// TrackedJob. Deduplicated by JobUrl against both the user's tracked jobs and their
    /// applications.
    /// </summary>
    Task<ExtensionTrackedJobResponse> CreateFromExtensionAsync(Guid userId, CreateFromExtensionRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid trackedJobId, CancellationToken cancellationToken);

    /// <summary>
    /// Turns a TrackedJob into a real Application once the user has actually applied, then removes
    /// the TrackedJob row — the job now lives in the Applications list instead.
    /// </summary>
    Task<ApplicationDetailResponse?> ConvertToApplicationAsync(Guid userId, Guid trackedJobId,
        ConvertTrackedJobRequest request, CancellationToken cancellationToken);
}
