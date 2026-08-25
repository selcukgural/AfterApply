using AfterApply.Application.Metrics;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.Metrics;

internal sealed class ProductMetricsService(AppDbContext dbContext, ILogger<ProductMetricsService> logger) : IProductMetricsService
{
    // Same set as AnalyticsService.RespondedStatuses/ReminderService.RespondedStatuses —
    // "responded" is defined once, reused here rather than redefined (DECISIONS.md).
    private static readonly HashSet<ApplicationStatus> RespondedStatuses =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview, ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Accepted
    ];

    private static readonly HashSet<ApplicationStatus> OutcomeStatuses =
    [
        ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn, ApplicationStatus.Ghosted
    ];

    public async Task<ProductMetricsSnapshot> ComputeSnapshotAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var users = await dbContext.Users
            .Select(u => new { u.Id, u.CreatedAt })
            .ToListAsync(cancellationToken);

        var applications = await dbContext.Applications
            .Select(a => new { a.Id, a.UserId, a.CompanyId, a.JobId, a.Status, a.CreatedAt, a.UpdatedAt })
            .ToListAsync(cancellationToken);

        var statusHistory = await dbContext.ApplicationStatusHistories
            .Where(h => h.FromStatus != null)
            .Select(h => new { h.ApplicationId, h.ChangedAt, h.ToStatus })
            .ToListAsync(cancellationToken);

        var totalUsers = users.Count;
        var activatedUserCount = applications.Select(a => a.UserId).Distinct().Count();
        var activationRate = totalUsers == 0 ? 0 : Rate(activatedUserCount, totalUsers);

        var sevenDaysAgo = now.AddDays(-7);
        var weeklyActiveUsers = applications.Where(a => a.UpdatedAt >= sevenDaysAgo).Select(a => a.UserId).Distinct().Count();

        var thirtyDaysAgo = now.AddDays(-30);
        var applicationsTracked30d = applications.Count(a => a.CreatedAt >= thirtyDaysAgo);
        var statusUpdates30d = statusHistory.Count(h => h.ChangedAt >= thirtyDaysAgo);

        var lastActivityByUser = applications
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Max(a => a.UpdatedAt));

        double? ComputeRetentionRate(int days)
        {
            var cohort = users.Where(u => now - u.CreatedAt >= TimeSpan.FromDays(days)).ToList();
            if (cohort.Count == 0)
            {
                return null;
            }

            var retained = cohort.Count(u =>
                lastActivityByUser.TryGetValue(u.Id, out var lastActivity) &&
                lastActivity >= u.CreatedAt + TimeSpan.FromDays(days));

            return Rate(retained, cohort.Count);
        }

        var d7 = ComputeRetentionRate(days: 7);
        var d30 = ComputeRetentionRate(days: 30);
        var d90 = ComputeRetentionRate(days: 90);

        var uniqueCompanies = applications.Select(a => a.CompanyId).Distinct().Count();
        var uniqueJobs = applications.Where(a => a.JobId is not null).Select(a => a.JobId).Distinct().Count();
        var applicationsWithOutcome = applications.Count(a => OutcomeStatuses.Contains(a.Status));
        var applicationsWithResponseTime = statusHistory
            .Where(h => RespondedStatuses.Contains(h.ToStatus))
            .Select(h => h.ApplicationId)
            .Distinct()
            .Count();

        var snapshot = new ProductMetricsSnapshot(
            totalUsers, activatedUserCount, activationRate, weeklyActiveUsers,
            applicationsTracked30d, statusUpdates30d, d7, d30, d90,
            applications.Count, uniqueCompanies, uniqueJobs, applicationsWithOutcome, applicationsWithResponseTime, now);

        logger.LogInformation(
            "Product metrics snapshot: TotalUsers={TotalUsers} ActivatedUsers={ActivatedUsers} " +
            "ActivationRate={ActivationRate} WeeklyActiveUsers={WeeklyActiveUsers} " +
            "ApplicationsTracked30d={ApplicationsTracked30d} StatusUpdates30d={StatusUpdates30d} " +
            "D7Retention={D7Retention} D30Retention={D30Retention} D90Retention={D90Retention} " +
            "TotalApplications={TotalApplications} UniqueCompanies={UniqueCompanies} UniqueJobs={UniqueJobs} " +
            "ApplicationsWithOutcome={ApplicationsWithOutcome} ApplicationsWithResponseTime={ApplicationsWithResponseTime}",
            snapshot.TotalUsers, snapshot.ActivatedUsers, snapshot.ActivationRate, snapshot.WeeklyActiveUsers,
            snapshot.ApplicationsTrackedLast30Days, snapshot.StatusUpdatesLast30Days,
            snapshot.D7RetentionRate, snapshot.D30RetentionRate, snapshot.D90RetentionRate,
            snapshot.TotalApplications, snapshot.UniqueCompanies, snapshot.UniqueJobs,
            snapshot.ApplicationsWithOutcome, snapshot.ApplicationsWithResponseTime);

        return snapshot;
    }

    private static double Rate(int count, int total) => total == 0 ? 0 : Math.Round(100.0 * count / total, 1);
}
