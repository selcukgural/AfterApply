using AfterApply.Application.Analytics;
using AfterApply.Application.Analytics.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Analytics;

internal sealed class AnalyticsService(AppDbContext dbContext) : IAnalyticsService
{
    // How far back the dashboard's application-volume trend reaches. Twelve weeks is the
    // widest window that still reads as individual bars in the sparkline's width.
    private const int TrendWeeks = 12;

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(Guid userId, CancellationToken cancellationToken)
    {
        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Id, a.Status, a.AppliedAt })
            .ToListAsync(cancellationToken);

        var total = applications.Count;

        // ApplicationStatusHistory has no navigation back to Application
        // (ApplicationConfiguration: HasMany(...).WithOne() with no inverse
        // configured), so the join must be explicit rather than h.Application.
        var historyRows = await dbContext.ApplicationStatusHistories
            .Join(dbContext.Applications.Where(a => a.UserId == userId),
                h => h.ApplicationId, a => a.Id,
                (h, a) => new { h.ApplicationId, h.ToStatus, h.ChangedAt, a.AppliedAt })
            .OrderBy(x => x.ChangedAt)
            .ToListAsync(cancellationToken);

        var respondedCount = 0;
        var interviewCount = 0;
        var offerCount = 0;
        var responseTimeDays = new List<double>();

        foreach (var group in historyRows.GroupBy(x => x.ApplicationId))
        {
            var firstResponse = group
                .Where(x => ApplicationStatusClassification.RespondedStatuses.Contains(x.ToStatus))
                .OrderBy(x => x.ChangedAt)
                .FirstOrDefault();

            if (firstResponse is not null)
            {
                respondedCount++;
                responseTimeDays.Add((firstResponse.ChangedAt - firstResponse.AppliedAt).TotalDays);
            }

            if (group.Any(x => ApplicationStatusClassification.InterviewStatuses.Contains(x.ToStatus)))
            {
                interviewCount++;
            }

            if (group.Any(x => ApplicationStatusClassification.OfferStatuses.Contains(x.ToStatus)))
            {
                offerCount++;
            }
        }

        var rejectedCount = applications.Count(a => a.Status == ApplicationStatus.Rejected);
        var ghostedCount = applications.Count(a => a.Status == ApplicationStatus.Ghosted);

        var rates = new AnalyticsRatesResponse(
            TotalApplications: total,
            RespondedCount: respondedCount,
            ResponseRate: AnalyticsCalculations.CalculateRate(respondedCount, total),
            InterviewCount: interviewCount,
            InterviewRate: AnalyticsCalculations.CalculateRate(interviewCount, total),
            OfferCount: offerCount,
            OfferRate: AnalyticsCalculations.CalculateRate(offerCount, total),
            RejectedCount: rejectedCount,
            RejectionRate: AnalyticsCalculations.CalculateRate(rejectedCount, total),
            GhostedCount: ghostedCount,
            GhostingRate: AnalyticsCalculations.CalculateRate(ghostedCount, total));

        var responseTime = new ResponseTimeStatsResponse(
            SampleSize: responseTimeDays.Count,
            AverageDays: AnalyticsCalculations.Average(responseTimeDays),
            MedianDays: AnalyticsCalculations.Median(responseTimeDays));

        var statusCounts = applications.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());
        var distribution = Enum.GetValues<ApplicationStatus>()
            .Select(s => new StatusDistributionItem(s, statusCounts.GetValueOrDefault(s)))
            .ToList();

        // Reuses the rows already materialised above — the trend costs no extra round trip.
        var applicationsPerWeek = AnalyticsCalculations.BuildWeeklyBuckets(
            applications.Select(a => a.AppliedAt), DateTimeOffset.UtcNow, TrendWeeks);

        return new AnalyticsOverviewResponse(rates, responseTime, distribution, applicationsPerWeek);
    }
}
