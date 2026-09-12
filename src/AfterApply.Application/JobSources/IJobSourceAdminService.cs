using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

public interface IJobSourceAdminService
{
    Task<UserJobSourceSettingsResponse> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserJobSourceSettingsResponse> UpdateUserLimitsAsync(Guid userId, UpdateUserJobSourceLimitsRequest request,
        CancellationToken cancellationToken);

    Task<JobSourceUsageResponse> GetUsageAsync(CancellationToken cancellationToken);
}
