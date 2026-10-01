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

        var responded = mature.Count(s => s.Responded);
        var ghosted = mature.Count(s => s.Status == ApplicationStatus.Ghosted);
        var interviewed = mature.Count(s => s.ReachedInterview);
        var offered = mature.Count(s => s.ReachedOffer);
        var closed = mature.Count(s => CompanyGivenClosureStatuses.Values.Contains(s.Status));
        var silentAfterInterview = mature.Count(s => s.ReachedInterview && s.Status == ApplicationStatus.Ghosted);

        // Every answered application counts here, young ones included: the reply happened. One
        // answered only by an import has no reply date (RespondedWithoutDate) and stays out.
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
    /// application's transitions in any order; only the target status and its time are read, and
    /// every row counts as a change the user made by hand.
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
        return ToSample(applicationId, userId, status, appliedAt,
            history.Select(h => new ResponseRateTransition(null, h.ToStatus, h.ChangedAt, StatusChangeOrigin.Manual)),
            promisedReplyBy, promisedReplySince, rejectionNotice, now);
    }

    /// <summary>
    /// The form the cross-user services call (DECISIONS.md 2026-10-01). Two things the origin
    /// changes:
    /// <list type="bullet">
    /// <item>A change the user undid — an unattended email auto-apply they reverted, a bulk edit
    /// they took back — never happened as far as the company is concerned: the pair is dropped
    /// before anything is read. An email suggestion the user confirmed, or one applied unattended
    /// and left standing, counts like any other change; it is the company's own email.</item>
    /// <item>An import's timestamp is the day of the import. A reply recorded only by an import
    /// still counts as a reply, but the application then has no reply time at all — taking the
    /// next hand-made change instead would stretch it by however long the user took to import.</item>
    /// </list>
    /// </summary>
    public static ResponseRateSample ToSample(
        Guid applicationId, Guid userId, ApplicationStatus status, DateTimeOffset appliedAt,
        IEnumerable<ResponseRateTransition> history,
        DateOnly? promisedReplyBy, DateTimeOffset? promisedReplySince, RejectionNotice? rejectionNotice,
        DateTimeOffset now)
    {
        var transitions = WithoutUndoneChanges(history);
        DateTimeOffset? firstResponded = null;
        var importedResponse = false;
        var reachedInterview = false;
        var reachedOffer = false;

        foreach (var transition in transitions)
        {
            if (ApplicationStatusClassification.RespondedStatuses.Contains(transition.ToStatus))
            {
                if (transition.Origin == StatusChangeOrigin.Import)
                {
                    importedResponse = true;
                }
                else if (firstResponded is null || transition.ChangedAt < firstResponded)
                {
                    firstResponded = transition.ChangedAt;
                }
            }

            reachedInterview |= ApplicationStatusClassification.InterviewStatuses.Contains(transition.ToStatus);
            reachedOffer |= ApplicationStatusClassification.OfferStatuses.Contains(transition.ToStatus);
        }

        var promise = promisedReplyBy is { } by && promisedReplySince is { } since
            ? ReplyPromises.Evaluate(by, since, transitions.Select(t => (t.ToStatus, t.ChangedAt)).ToList(), now)
            : null;

        return new ResponseRateSample(applicationId, userId, status, appliedAt,
            importedResponse ? null : firstResponded, reachedInterview, reachedOffer,
            promise, status == ApplicationStatus.Rejected ? rejectionNotice : null,
            RespondedWithoutDate: importedResponse);
    }

    /// <summary>The history in time order with every undo and the change it undid taken out. An
    /// undo row names the status it reverses as its FromStatus; it cancels the latest earlier row
    /// of the matching kind that moved into that status.</summary>
    private static List<ResponseRateTransition> WithoutUndoneChanges(IEnumerable<ResponseRateTransition> history)
    {
        var kept = new List<ResponseRateTransition>();
        foreach (var transition in history.OrderBy(h => h.ChangedAt))
        {
            StatusChangeOrigin? undoes = transition.Origin switch
            {
                StatusChangeOrigin.EmailAutoApplyReverted => StatusChangeOrigin.EmailAutoApplied,
                StatusChangeOrigin.BulkEditReverted => StatusChangeOrigin.BulkEdit,
                _ => null
            };

            if (undoes is null)
            {
                kept.Add(transition);
                continue;
            }

            var undone = kept.FindLastIndex(k => k.Origin == undoes && k.ToStatus == transition.FromStatus);
            if (undone >= 0)
            {
                kept.RemoveAt(undone);
            }
        }

        return kept;
    }
}
