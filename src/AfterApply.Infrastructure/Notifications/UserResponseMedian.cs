using AfterApply.Application.Notifications;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Notifications;

/// <summary>
/// How long companies usually take to answer this user: days from AppliedAt to the first
/// "responded" status transition, per answered application, reduced to the median by
/// <see cref="ReminderCalculations.UserMedianResponseDays"/> (null below its minimum sample). The
/// same definition of "first reply" as AnalyticsService.GetOverviewAsync, computed in SQL because
/// the reminders page and the application detail only need one number back.
/// </summary>
internal static class UserResponseMedian
{
    public static async Task<int?> GetAsync(AppDbContext dbContext, Guid userId, CancellationToken cancellationToken)
    {
        var firstReplies = await dbContext.ApplicationStatusHistories
            .Join(dbContext.Applications.Where(a => a.UserId == userId),
                h => h.ApplicationId, a => a.Id,
                (h, a) => new { h.ApplicationId, h.ToStatus, h.ChangedAt, a.AppliedAt })
            .Where(x => ApplicationStatusClassification.RespondedStatuses.Contains(x.ToStatus))
            .GroupBy(x => new { x.ApplicationId, x.AppliedAt })
            .Select(g => new { g.Key.AppliedAt, FirstReplyAt = g.Min(x => x.ChangedAt) })
            .ToListAsync(cancellationToken);

        var days = firstReplies
            .Select(x => (x.FirstReplyAt - x.AppliedAt).TotalDays)
            .ToList();

        return ReminderCalculations.UserMedianResponseDays(days);
    }
}
