using AfterApply.Domain.Applications;

namespace AfterApply.Application.ResponseRates;

/// <summary>
/// One application reduced to what the response-rate figures need — the same shape whether the
/// aggregate is one company or a whole sector. The status history is already folded in: the
/// service that loads the rows computes <see cref="FirstRespondedAt"/> and the two "reached"
/// flags once, and <see cref="ResponseRateAggregator"/> never sees a history row.
/// </summary>
/// <param name="UserId">Who submitted it — only ever counted (distinct contributors, the largest
/// single share); never exposed.</param>
/// <param name="FirstRespondedAt">When the company first did anything a candidate would call a
/// reply (<see cref="ApplicationStatusClassification.RespondedStatuses"/>), or null if it never
/// has.</param>
/// <param name="Promise">The company's reply promise, already read by
/// <see cref="ReplyPromises.Evaluate"/>; null when none was recorded.</param>
/// <param name="RejectionNotice">How the candidate learned of the rejection; null unless the
/// application is Rejected and they said.</param>
public sealed record ResponseRateSample(
    Guid ApplicationId,
    Guid UserId,
    ApplicationStatus Status,
    DateTimeOffset AppliedAt,
    DateTimeOffset? FirstRespondedAt,
    bool ReachedInterview,
    bool ReachedOffer,
    ReplyPromiseEvaluation? Promise = null,
    RejectionNotice? RejectionNotice = null);
