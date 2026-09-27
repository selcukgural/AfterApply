using AfterApply.Application.FeatureFlags.Contracts;

namespace AfterApply.Application.FeatureFlags;

/// <summary>
/// The admin side of <see cref="IFeatureFlags"/>: list, history, and a change in two confirmed
/// steps — <see cref="PrepareAsync"/> spells the change out and issues a token,
/// <see cref="ConfirmAsync"/> applies it only when that same admin sends the token back together
/// with the flag's name typed out. Callers check admin access first; nothing here does.
/// </summary>
public interface IFeatureFlagAdminService
{
    Task<IReadOnlyList<FeatureFlagResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>First step. Changes nothing. Switching a flag on is refused while its prerequisite
    /// is missing; switching off and resetting never are.</summary>
    Task<PrepareFeatureFlagChangeResult> PrepareAsync(FeatureFlag flag, bool? enabled, string reason, Guid adminId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Second step: applies the prepared change and records it with the admin and the connection's
    /// IP. Every instance sees it within a second or two (Redis pub/sub), within the poll interval
    /// if Redis is down.
    /// </summary>
    Task<ConfirmFeatureFlagChangeResult> ConfirmAsync(FeatureFlag flag, string token, string phrase, Guid adminId,
        string? ipAddress, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeatureFlagChangeResponse>> GetHistoryAsync(FeatureFlag? flag, int limit,
        CancellationToken cancellationToken);
}
