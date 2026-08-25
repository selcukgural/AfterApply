using AfterApply.Domain.Applications;

namespace AfterApply.Application.Notifications;

public static class ReminderCalculations
{
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
}
