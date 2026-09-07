using AfterApply.Application.EmailIntegrations.Contracts;

namespace AfterApply.Application.EmailIntegrations;

public interface IAutoApprovalCalibrationService
{
    /// <summary>Accuracy by confidence band for the auto-approval path, across all users. Aggregate
    /// counts only — no addresses, subjects or company names leave this method.</summary>
    Task<AutoApprovalCalibrationResponse> ComputeAsync(CancellationToken cancellationToken);
}
