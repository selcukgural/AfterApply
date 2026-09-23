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

/// <summary><paramref name="WeekStart"/> is the Monday (UTC) the bucket opens on.</summary>
public sealed record ApplicationsPerWeekItem(DateOnly WeekStart, int Count);

public sealed record AnalyticsOverviewResponse(
    AnalyticsRatesResponse Rates,
    ResponseTimeStatsResponse ResponseTime,
    IReadOnlyCollection<StatusDistributionItem> StatusDistribution,
    IReadOnlyCollection<ApplicationsPerWeekItem> ApplicationsPerWeek);

/// <summary>
/// The shareable flow card's nodes (ApplicationFlowClassifier). First column: Unanswered +
/// AwaitingReply + RejectedBeforeInterview + InScreening + WithdrawnBeforeInterview + Interviewed
/// = Total. Second column, out of Interviewed: Offer + InterviewInProgress +
/// RejectedAfterInterview + SilentAfterInterview + WithdrawnAfterInterview = Interviewed.
/// </summary>
public sealed record ApplicationFlowCounts(
    int Total,
    int Unanswered,
    int AwaitingReply,
    int RejectedBeforeInterview,
    int InScreening,
    int WithdrawnBeforeInterview,
    int Interviewed,
    int Offer,
    int InterviewInProgress,
    int RejectedAfterInterview,
    int SilentAfterInterview,
    int WithdrawnAfterInterview);

/// <summary>
/// Counts only — no company, role or date of any single application — because this is what a
/// person puts on a public card. <paramref name="FirstAppliedOn"/> and <paramref name="Today"/>
/// give the card its month range; <paramref name="MedianFirstReplyDays"/> is null when nothing in
/// the window was answered.
/// </summary>
public sealed record ApplicationFlowResponse(
    ApplicationFlowCounts Counts,
    double? MedianFirstReplyDays,
    DateOnly? FirstAppliedOn,
    DateOnly Today);
