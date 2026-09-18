using AfterApply.Application.Analytics;
using AfterApply.Domain.Applications;

namespace AfterApply.Application.Notifications;

public static class ReminderCalculations
{
    /// <summary>
    /// How many of the user's applications must have been answered before their own median reply
    /// time is shown next to a "possibly ghosted" row. Below this a median is one or two data
    /// points wearing a statistic's clothes — "you usually hear back in 2 days" off a single fast
    /// rejection would set an expectation the next thirty days then fail.
    /// </summary>
    public const int MedianMinimumSampleSize = 3;

    /// <summary>
    /// The user's own median first-reply time in whole days, or null when fewer than
    /// <see cref="MedianMinimumSampleSize"/> applications have been answered. Same median as the
    /// dashboard's response-time card (AnalyticsCalculations.Median), rounded because the sentence
    /// it feeds — "you usually hear back in 9 days" — has no use for a half day.
    /// </summary>
    public static int? UserMedianResponseDays(IReadOnlyCollection<double> firstReplyDays)
    {
        if (firstReplyDays.Count < MedianMinimumSampleSize)
        {
            return null;
        }

        var median = AnalyticsCalculations.Median(firstReplyDays);
        return median is null ? null : (int)Math.Round(median.Value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The reference date staleness is measured from: the most recent real status
    /// transition (FromStatus != null), falling back to AppliedAt when the
    /// application has only its seed history row (FromStatus == null, added by
    /// Application.Create). Using AppliedAt directly instead of the seed row's
    /// ChangedAt matters for backdated applications (CSV/LinkedIn import).
    /// </summary>
    public static DateTimeOffset GetReferenceAt(DateTimeOffset appliedAt,
        IEnumerable<(ApplicationStatus? FromStatus, DateTimeOffset ChangedAt)> history)
    {
        DateTimeOffset? latest = null;

        foreach (var (fromStatus, changedAt) in history)
        {
            if (fromStatus is null)
            {
                continue;
            }

            if (latest is null || changedAt > latest)
            {
                latest = changedAt;
            }
        }

        return latest ?? appliedAt;
    }

    public static int DaysElapsed(DateTimeOffset referenceAt, DateTimeOffset now)
    {
        return (int)(now - referenceAt).TotalDays;
    }

    public static bool IsFollowUpDue(int daysElapsed, int followUpThresholdDays)
    {
        return daysElapsed >= followUpThresholdDays;
    }

    public static bool IsPossiblyGhosted(bool hasResponded, int daysElapsed, int ghostingThresholdDays)
    {
        return !hasResponded && daysElapsed >= ghostingThresholdDays;
    }

    /// <summary>
    /// Past the horizon nothing is a reminder any more — see NotificationOptions.StaleThresholdDays.
    /// Applies to both types: a follow-up on an interview that went quiet a year ago is as
    /// unactionable as a "possibly ghosted" on a 2017 import.
    /// </summary>
    public static bool IsBeyondHorizon(int daysElapsed, int staleThresholdDays)
    {
        return daysElapsed >= staleThresholdDays;
    }
}
