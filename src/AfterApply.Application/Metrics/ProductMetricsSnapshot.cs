namespace AfterApply.Application.Metrics;

public sealed record ProductMetricsSnapshot(
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
