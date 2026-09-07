namespace AfterApply.Application.EmailIntegrations.Contracts;

/// <summary>
/// One confidence band's worth of evidence about whether auto-apply can be trusted at that band.
///
/// Both rates are here because they answer different questions and only one of them is honest.
/// </summary>
public sealed record AutoApprovalCalibrationBucket(
    double LowerBound,
    double UpperBound,
    int Total,
    int Confirmed,
    int Dismissed,
    int AutoApplied,
    int Reverted,
    int Pending,

    /// <summary>
    /// Confirmed / (Confirmed + Dismissed) — how often the user agreed with a suggestion they were
    /// <i>shown</i>.
    ///
    /// Read it with suspicion. Confirming is a low-friction "yes" to a question that was asked;
    /// people say yes to things they would have objected to had the product done them unattended.
    /// This number therefore flatters auto-apply, and no amount of extra data fixes that — the bias
    /// is in what is being measured, not in the sample size.
    /// </summary>
    double? AgreementRate,

    /// <summary>
    /// Reverted / AutoApplied — how often an unattended apply was taken back.
    ///
    /// This is the number that actually settles the threshold: the user is reacting to something
    /// that really did happen without being asked. Null until auto-apply has been enabled and has
    /// acted in this band, which is the honest reading — before that, nothing has measured it.
    /// </summary>
    double? RevertRate);

/// <summary>
/// Accuracy by confidence band for the email auto-approval path, computed from the suggestions
/// already stored — no separate telemetry, and retroactive for any threshold.
///
/// Counts only suggestions that could ever qualify for auto-apply (a DomainMatch classified by the
/// LLM path), which is the qualifying rule from EmailForwardingService.TryAutoApplyAsync minus the
/// threshold itself — the point being to see what a different threshold would have done.
/// </summary>
public sealed record AutoApprovalCalibrationResponse(
    double CurrentThreshold,
    bool AutoApplyEnabled,
    bool ShadowModeEnabled,
    int QualifyingTotal,
    IReadOnlyList<AutoApprovalCalibrationBucket> Buckets);
