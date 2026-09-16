using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

public interface IUserJobSourceDeliveryService
{
    /// <param name="weekKey">Null means the most recent week the user has a run for.</param>
    Task<JobSourceDeliveriesResponse> ListAsync(Guid userId, int? weekKey, int take, CancellationToken cancellationToken);

    /// <summary>Null unless the posting was delivered to this user — a posting id alone opens nothing.</summary>
    Task<JobSourcePostingDetailResponse?> GetAsync(Guid userId, Guid postingId, CancellationToken cancellationToken);

    /// <summary>"Başvurdum": records an application for a delivered posting through the ordinary
    /// application flow, dated now. Idempotent on the posting's URL — a second click returns the
    /// application already recorded. Null unless the posting was delivered to this user.</summary>
    Task<ApplicationDetailResponse?> MarkAppliedAsync(Guid userId, Guid postingId, CancellationToken cancellationToken);

    /// <summary>The page's opening question: paying, CV present, criteria saved.</summary>
    Task<JobSourceStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Closes the dashboard's announcement of the feature for good, for this account.</summary>
    Task DismissAnnouncementAsync(Guid userId, CancellationToken cancellationToken);
}
