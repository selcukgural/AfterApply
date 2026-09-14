using AfterApply.Application.Common;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

public interface IUserJobSourceProfileService
{
    Task<JobSourceProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<JobSourceProfileResponse> UpsertAsync(Guid userId, UpsertJobSourceProfileRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>A profile cannot be created without consent to the CV going to the scoring model —
/// there is no version of this feature that works without it.</summary>
public sealed class JobSourceAiConsentRequiredException()
    : CodedException("JOB_SOURCE_AI_CONSENT_REQUIRED", "Consent to AI scoring is required to save job-search criteria.");
