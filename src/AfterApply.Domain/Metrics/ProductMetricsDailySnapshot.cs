using AfterApply.Domain.Common;

namespace AfterApply.Domain.Metrics;

/// <summary>
/// One day's product-health numbers, frozen. The daily job computed these from the start but only
/// wrote them to a log line, so nothing could be compared against last week — and "is activation
/// getting better?" is the only form the question is ever actually asked in. This row is what makes
/// the answer possible.
///
/// Deliberately *not* user-owned: every field is a count or a rate across the whole product, there
/// is no UserId, and so this table needs none of the cascade-from-Users wiring that
/// FeedbackEntryConfiguration and its siblings carry (see DECISIONS.md 2026-09-07, account
/// deletion). Deleting an account changes tomorrow's numbers, never yesterday's row.
///
/// Recomputing a day overwrites it — see ProductMetricsService.ComputeSnapshotAsync — so a job that
/// runs twice, or is triggered by hand, leaves one row per day rather than a duplicate.
/// </summary>
public sealed class ProductMetricsDailySnapshot : Entity
{
    /// <summary>The UTC day these numbers describe. Unique — the upsert key.</summary>
    public DateOnly SnapshotDate { get; private set; }

    public int TotalUsers { get; private set; }

    /// <summary>Users who have at least one application. The denominator of everything else:
    /// someone who registered and never tracked anything has not used the product.</summary>
    public int ActivatedUsers { get; private set; }

    public double ActivationRate { get; private set; }

    public int WeeklyActiveUsers { get; private set; }

    public int ApplicationsTrackedLast30Days { get; private set; }

    public int StatusUpdatesLast30Days { get; private set; }

    /// <summary>Null when no cohort is old enough to answer yet — not zero. A young product has no
    /// D90 retention, and recording that as 0% would read as "everybody left".</summary>
    public double? D7RetentionRate { get; private set; }

    public double? D30RetentionRate { get; private set; }

    public double? D90RetentionRate { get; private set; }

    public int TotalApplications { get; private set; }

    public int UniqueCompanies { get; private set; }

    public int UniqueJobs { get; private set; }

    public int ApplicationsWithOutcome { get; private set; }

    public int ApplicationsWithResponseTime { get; private set; }

    /// <summary>When the job actually ran, as opposed to which day it describes.</summary>
    public DateTimeOffset ComputedAt { get; private set; }

    private ProductMetricsDailySnapshot()
    {
    }

    public static ProductMetricsDailySnapshot Create(DateOnly snapshotDate)
    {
        return new ProductMetricsDailySnapshot { SnapshotDate = snapshotDate };
    }

    /// <summary>
    /// Overwrites every measured field. Used for both the first write of a day and a recompute of
    /// the same day, which is why it takes the whole set rather than exposing setters — a snapshot
    /// is meaningful only as one internally-consistent reading.
    /// </summary>
    public void Record(
        int totalUsers, int activatedUsers, double activationRate, int weeklyActiveUsers,
        int applicationsTrackedLast30Days, int statusUpdatesLast30Days,
        double? d7RetentionRate, double? d30RetentionRate, double? d90RetentionRate,
        int totalApplications, int uniqueCompanies, int uniqueJobs,
        int applicationsWithOutcome, int applicationsWithResponseTime, DateTimeOffset computedAt)
    {
        TotalUsers = totalUsers;
        ActivatedUsers = activatedUsers;
        ActivationRate = activationRate;
        WeeklyActiveUsers = weeklyActiveUsers;
        ApplicationsTrackedLast30Days = applicationsTrackedLast30Days;
        StatusUpdatesLast30Days = statusUpdatesLast30Days;
        D7RetentionRate = d7RetentionRate;
        D30RetentionRate = d30RetentionRate;
        D90RetentionRate = d90RetentionRate;
        TotalApplications = totalApplications;
        UniqueCompanies = uniqueCompanies;
        UniqueJobs = uniqueJobs;
        ApplicationsWithOutcome = applicationsWithOutcome;
        ApplicationsWithResponseTime = applicationsWithResponseTime;
        ComputedAt = computedAt;
    }
}
