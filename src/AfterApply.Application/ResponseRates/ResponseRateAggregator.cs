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
            ClosureRate: AnalyticsCalculations.CalculateRate(closed, mature.Count));
    }

    /// <summary>
    /// Folds an application's status history into one sample. <paramref name="history"/> is that
    /// application's transitions in any order; only the target status and its time are read.
    /// </summary>
    public static ResponseRateSample ToSample(
        Guid applicationId, Guid userId, ApplicationStatus status, DateTimeOffset appliedAt,
        IEnumerable<(ApplicationStatus ToStatus, DateTimeOffset ChangedAt)> history)
    {
        DateTimeOffset? firstResponded = null;
        var reachedInterview = false;
        var reachedOffer = false;

        foreach (var (toStatus, changedAt) in history)
        {
            if (ApplicationStatusClassification.RespondedStatuses.Contains(toStatus)
                && (firstResponded is null || changedAt < firstResponded))
            {
                firstResponded = changedAt;
            }

            reachedInterview |= ApplicationStatusClassification.InterviewStatuses.Contains(toStatus);
            reachedOffer |= ApplicationStatusClassification.OfferStatuses.Contains(toStatus);
        }

        return new ResponseRateSample(applicationId, userId, status, appliedAt, firstResponded, reachedInterview, reachedOffer);
    }
}
