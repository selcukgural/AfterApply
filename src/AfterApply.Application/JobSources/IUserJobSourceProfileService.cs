using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

public interface IUserJobSourceProfileService
{
    Task<JobSourceProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<JobSourceProfileResponse> UpsertAsync(Guid userId, UpsertJobSourceProfileRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
