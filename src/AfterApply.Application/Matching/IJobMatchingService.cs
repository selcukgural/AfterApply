using AfterApply.Application.Matching.Contracts;

namespace AfterApply.Application.Matching;

public interface IJobMatchingService
{
    Task<CandidateProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<CandidateProfileResponse> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request, CancellationToken cancellationToken);

    Task<JobMatchResponse?> GetMatchAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the cached JobMatch when the given job description and the user's current CV
    /// match the snapshot already stored for this application; otherwise calls the AI provider
    /// and persists the new result. Returns null when the application doesn't belong to the
    /// user (endpoint maps that to 404); throws a CodedException when the user has no
    /// CandidateProfile yet (endpoint maps that to 400).
    /// </summary>
    Task<JobMatchResponse?> ComputeMatchAsync(Guid userId, Guid applicationId, ComputeJobMatchRequest request, CancellationToken cancellationToken);
}
