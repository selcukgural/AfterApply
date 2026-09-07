namespace AfterApply.Application.Metrics;

/// <summary>One stored day of product metrics, as the admin surface reads it back. Same fields as
/// <see cref="ProductMetricsSnapshot"/> plus the day they describe — the snapshot record is what a
/// single run computes, this is what a run left behind.</summary>
public sealed record ProductMetricsDayResponse(
    DateOnly SnapshotDate,
    int TotalUsers,
    int ActivatedUsers,
    double ActivationRate,
    int WeeklyActiveUsers,
    int ApplicationsTrackedLast30Days,
    int StatusUpdatesLast30Days,
    double? D7RetentionRate,
    double? D30RetentionRate,
    double? D90RetentionRate,
    int TotalApplications,
    int UniqueCompanies,
    int UniqueJobs,
    int ApplicationsWithOutcome,
    int ApplicationsWithResponseTime,
    DateTimeOffset ComputedAt);
