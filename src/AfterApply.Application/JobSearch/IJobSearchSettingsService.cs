using AfterApply.Application.JobSearch.Contracts;

namespace AfterApply.Application.JobSearch;

/// <summary>Per-user job search settings. The "mine" pair is the user's own preferences; the
/// "for user" pair is the admin's limit overrides. See JobSearchUserSettings for why they are
/// separate paths.</summary>
public interface IJobSearchSettingsService
{
    Task<EffectiveJobSearchSettings> ResolveAsync(Guid userId, CancellationToken cancellationToken);

    Task<JobSearchSettingsResponse> GetMineAsync(Guid userId, CancellationToken cancellationToken);

    Task<JobSearchSettingsResponse> UpdateMineAsync(Guid userId, UpdateJobSearchPreferencesRequest request, CancellationToken cancellationToken);

    /// <summary>Null when <paramref name="targetUserId"/> is not an account.</summary>
    Task<JobSearchSettingsResponse?> GetForUserAsync(Guid targetUserId, CancellationToken cancellationToken);

    /// <summary>Null when <paramref name="targetUserId"/> is not an account.</summary>
    Task<JobSearchSettingsResponse?> UpdateLimitsAsync(Guid targetUserId, UpdateJobSearchLimitsRequest request, CancellationToken cancellationToken);
}
