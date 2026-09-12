using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

public interface IUserJobSourceDeliveryService
{
    /// <param name="weekKey">Null means the most recent week the user has a run for.</param>
    Task<JobSourceDeliveriesResponse> ListAsync(Guid userId, int? weekKey, int take, CancellationToken cancellationToken);

    /// <summary>Null unless the posting was delivered to this user — a posting id alone opens nothing.</summary>
    Task<JobSourcePostingDetailResponse?> GetAsync(Guid userId, Guid postingId, CancellationToken cancellationToken);
}
