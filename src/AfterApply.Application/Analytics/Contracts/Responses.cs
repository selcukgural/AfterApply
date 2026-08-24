using AfterApply.Domain.Applications;

namespace AfterApply.Application.Analytics.Contracts;

public sealed record StatusDistributionItem(ApplicationStatus Status, int Count);

public sealed record AnalyticsRatesResponse(
    int TotalApplications,
    int RespondedCount,
    double ResponseRate,
    int InterviewCount,
    double InterviewRate,
    int OfferCount,
    double OfferRate,
    int RejectedCount,
    double RejectionRate,
    int GhostedCount,
    double GhostingRate);

public sealed record ResponseTimeStatsResponse(int SampleSize, double? AverageDays, double? MedianDays);

public sealed record AnalyticsOverviewResponse(
    AnalyticsRatesResponse Rates,
    ResponseTimeStatsResponse ResponseTime,
    IReadOnlyCollection<StatusDistributionItem> StatusDistribution);
