namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Gates fully-automatic Application status changes from a high-confidence, strongly-matched,
/// LLM-classified email suggestion — see EmailForwardingService.TryAutoApplyAsync. Both
/// ShadowModeEnabled and ConfidenceThreshold exist because there is no calibration data yet for what
/// confidence is actually safe to auto-act on (no accuracy-by-confidence-bucket eval exists in this
/// repo) — ship in shadow mode first, review logged "would-have-auto-applied" decisions, then tune
/// ConfidenceThreshold and flip Enabled. See DECISIONS.md.</summary>
public sealed class EmailAutoApprovalOptions
{
    /// <summary>Master switch for real auto-apply (actually calls ChangeStatusAsync). Default false —
    /// must be deliberately turned on after reviewing shadow-mode logs.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>When true (and Enabled is false), qualifying suggestions are logged as "would auto
    /// apply" without mutating anything. Default true.</summary>
    public bool ShadowModeEnabled { get; init; } = true;

    /// <summary>Minimum ConfidenceScore (LLM-path only — rule-based confidence is a hand-tuned weight,
    /// never a calibrated probability, so it never qualifies regardless of this value) required to
    /// auto-apply. Conservative placeholder, not calibrated against real outcomes yet.</summary>
    public double ConfidenceThreshold { get; init; } = 0.9;
}
