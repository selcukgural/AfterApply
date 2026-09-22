using AfterApply.Application.Analytics;
using AfterApply.Domain.Applications;

namespace AfterApply.Application.ResponseRates;

/// <summary>
/// The one place the response-rate figures are computed, for a company (CompanyIntelligence)
/// and for a sector (the public response-rates page) alike — the two must never disagree on
/// what "response rate" means, and the way to guarantee that is a single function. Pure: no
/// clock, no store; the caller passes <paramref name="now"/>.
/// </summary>
public static class ResponseRateAggregator
{
    /// <summary>
    /// The floor under each of the two self-reported sub-rates — promise keeping and rejection
    /// notice. They are asked, optionally, so they come from far fewer applications than the row
    /// they sit in; a row that clears its own threshold can still hold the promise record of one
    /// person. Five answers from three different people is the least that reads as a pattern
    /// rather than an anecdote; below it the rate is null and the page prints a dash.
    /// </summary>
    public const int SubRateMinimumSamples = 5;

    public const int SubRateMinimumContributors = 3;

    public static ResponseRateFigures Compute(IReadOnlyList<ResponseRateSample> samples, DateTimeOffset now, int maturityDays)
    {
        var total = samples.Count;
        var contributors = samples.Select(s => s.UserId).Distinct().Count();
        var maxShare = total == 0
            ? 0
            : (double)samples.GroupBy(s => s.UserId).Max(g => g.Count()) / total;

        var maturityCutoff = now.AddDays(-maturityDays);
        var mature = samples.Where(s => s.AppliedAt <= maturityCutoff).ToList();

        var responded = mature.Count(s => s.FirstRespondedAt is not null);
        var ghosted = mature.Count(s => s.Status == ApplicationStatus.Ghosted);
        var interviewed = mature.Count(s => s.ReachedInterview);
        var offered = mature.Count(s => s.ReachedOffer);
        var closed = mature.Count(s => CompanyGivenClosureStatuses.Values.Contains(s.Status));
        var silentAfterInterview = mature.Count(s => s.ReachedInterview && s.Status == ApplicationStatus.Ghosted);

        // Every answered application counts here, young ones included: the reply happened.
        var replyDays = samples
            .Where(s => s.FirstRespondedAt is not null)
            .Select(s => (s.FirstRespondedAt!.Value - s.AppliedAt).TotalDays)
            .ToList();

        var settledPromises = samples
            .Where(s => s.Promise is { } p && (p.CountsAsKept || p.CountsAsBroken))
            .ToList();
        var knownRejections = samples
            .Where(s => s.Status == ApplicationStatus.Rejected && s.RejectionNotice is not null)
            .ToList();

        return new ResponseRateFigures(
            TotalApplications: total,
            MatureApplications: mature.Count,
            DistinctContributors: contributors,
            MaxContributorShare: Math.Round(maxShare, 3),
            ResponseRate: AnalyticsCalculations.CalculateRate(responded, mature.Count),
            GhostingRate: AnalyticsCalculations.CalculateRate(ghosted, mature.Count),
            InterviewRate: AnalyticsCalculations.CalculateRate(interviewed, mature.Count),
            OfferRate: AnalyticsCalculations.CalculateRate(offered, mature.Count),
            PostInterviewSilenceRate: interviewed == 0
                ? null
                : AnalyticsCalculations.CalculateRate(silentAfterInterview, interviewed),
            AverageFirstReplyDays: AnalyticsCalculations.Average(replyDays),
            MedianFirstReplyDays: AnalyticsCalculations.Median(replyDays),
            ClosureRate: AnalyticsCalculations.CalculateRate(closed, mature.Count),
            PromiseKeptRate: SubRate(settledPromises, s => s.Promise!.CountsAsKept),
            PromiseSamples: settledPromises.Count,
            RejectionNoticeRate: SubRate(knownRejections, s => s.RejectionNotice == RejectionNotice.CompanyNotified),
            RejectionNoticeSamples: knownRejections.Count);
    }

    private static double? SubRate(IReadOnlyCollection<ResponseRateSample> answered, Func<ResponseRateSample, bool> isYes)
    {
        if (answered.Count < SubRateMinimumSamples
            || answered.Select(s => s.UserId).Distinct().Count() < SubRateMinimumContributors)
        {
            return null;
        }

        return AnalyticsCalculations.CalculateRate(answered.Count(isYes), answered.Count);
    }

    /// <summary>
    /// Folds an application's status history into one sample. <paramref name="history"/> is that
    /// application's transitions in any order; only the target status and its time are read.
    /// </summary>
    public static ResponseRateSample ToSample(
        Guid applicationId, Guid userId, ApplicationStatus status, DateTimeOffset appliedAt,
        IEnumerable<(ApplicationStatus ToStatus, DateTimeOffset ChangedAt)> history)
    {
        return ToSample(applicationId, userId, status, appliedAt, history,
            promisedReplyBy: null, promisedReplySince: null, rejectionNotice: null, now: default);
    }

    /// <summary>
    /// As above, plus the two self-reported answers. <paramref name="now"/> settles an unanswered
    /// promise whose date and grace have run out; it is only read when a promise is present.
    /// </summary>
    public static ResponseRateSample ToSample(
        Guid applicationId, Guid userId, ApplicationStatus status, DateTimeOffset appliedAt,
        IEnumerable<(ApplicationStatus ToStatus, DateTimeOffset ChangedAt)> history,
        DateOnly? promisedReplyBy, DateTimeOffset? promisedReplySince, RejectionNotice? rejectionNotice,
        DateTimeOffset now)
    {
        var transitions = history as IReadOnlyCollection<(ApplicationStatus ToStatus, DateTimeOffset ChangedAt)>
                          ?? history.ToList();
        DateTimeOffset? firstResponded = null;
        var reachedInterview = false;
        var reachedOffer = false;

        foreach (var (toStatus, changedAt) in transitions)
        {
            if (ApplicationStatusClassification.RespondedStatuses.Contains(toStatus)
                && (firstResponded is null || changedAt < firstResponded))
            {
                firstResponded = changedAt;
            }

            reachedInterview |= ApplicationStatusClassification.InterviewStatuses.Contains(toStatus);
            reachedOffer |= ApplicationStatusClassification.OfferStatuses.Contains(toStatus);
        }

        var promise = promisedReplyBy is { } by && promisedReplySince is { } since
            ? ReplyPromises.Evaluate(by, since, transitions, now)
            : null;

        return new ResponseRateSample(applicationId, userId, status, appliedAt, firstResponded, reachedInterview, reachedOffer,
            promise, status == ApplicationStatus.Rejected ? rejectionNotice : null);
    }
}
