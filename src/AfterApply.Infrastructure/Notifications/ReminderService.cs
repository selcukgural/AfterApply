using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Notifications;

internal sealed class ReminderService(AppDbContext dbContext, IOptions<NotificationOptions> options, HybridCache cache) : IReminderService
{
    // Same set as AnalyticsService.RespondedStatuses — "responded" is defined once,
    // reused here rather than redefined (DECISIONS.md).
    private static readonly HashSet<ApplicationStatus> RespondedStatuses =
    [
        ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.TechnicalInterview,
        ApplicationStatus.FinalInterview, ApplicationStatus.Offer, ApplicationStatus.Rejected, ApplicationStatus.Accepted
    ];

    private static readonly HybridCacheEntryOptions ActiveRemindersCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(20),
        LocalCacheExpiration = TimeSpan.FromSeconds(20)
    };

    private static string ActiveRemindersCacheKey(Guid userId) => $"reminders:active:{userId}";

    public Task<IReadOnlyList<ReminderResponse>> GetActiveRemindersAsync(Guid userId, CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            ActiveRemindersCacheKey(userId),
            userId,
            async (uid, ct) => (IReadOnlyList<ReminderResponse>)await dbContext.Reminders
                .Where(r => r.UserId == uid && r.DismissedAt == null)
                .Join(dbContext.Applications, r => r.ApplicationId, a => a.Id,
                    (r, a) => new { r, a.CompanyId, a.JobTitle })
                .Join(dbContext.Companies, x => x.CompanyId, c => c.Id,
                    (x, c) => new { x.r, x.JobTitle, CompanyName = c.Name })
                .OrderByDescending(x => x.r.CreatedAt)
                .Select(x => new ReminderResponse(
                    x.r.Id, x.r.ApplicationId, x.CompanyName, x.JobTitle, x.r.Type, x.r.DaysElapsedAtCreation, x.r.CreatedAt))
                .ToListAsync(ct),
            ActiveRemindersCacheOptions,
            cancellationToken: cancellationToken).AsTask();
    }

    public async Task<bool> DismissAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == userId, cancellationToken);

        if (reminder is null)
        {
            return false;
        }

        reminder.Dismiss(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ActiveRemindersCacheKey(userId), cancellationToken);

        return true;
    }

    public async Task<int> ScanAndGenerateRemindersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var applications = await dbContext.Applications
            .Where(a => !TerminalApplicationStatuses.Values.Contains(a.Status))
            .Select(a => new { a.Id, a.UserId, a.AppliedAt })
            .ToListAsync(cancellationToken);

        if (applications.Count == 0)
        {
            return 0;
        }

        var applicationIds = applications.Select(a => a.Id).ToList();

        var historyRows = await dbContext.ApplicationStatusHistories
            .Where(h => applicationIds.Contains(h.ApplicationId))
            .Select(h => new { h.ApplicationId, h.FromStatus, h.ToStatus, h.ChangedAt })
            .ToListAsync(cancellationToken);

        var historyByApplication = historyRows.ToLookup(h => h.ApplicationId);

        var candidates = new List<(Guid ApplicationId, Guid UserId, ReminderType Type, DateTimeOffset ReferenceAt, int DaysElapsed)>();

        foreach (var application in applications)
        {
            var history = historyByApplication[application.Id].ToList();
            var hasResponded = history.Any(h => RespondedStatuses.Contains(h.ToStatus));
            var referenceAt = ReminderCalculations.GetReferenceAt(application.AppliedAt,
                history.Select(h => (h.FromStatus, h.ChangedAt)));
            var daysElapsed = ReminderCalculations.DaysElapsed(referenceAt, now);

            // Ghosting takes precedence: an application eligible for both never
            // surfaces both suggestions at once (product decision, Sprint 6 plan).
            ReminderType? type = ReminderCalculations.IsPossiblyGhosted(hasResponded, daysElapsed, options.Value.GhostingThresholdDays)
                ? ReminderType.PossiblyGhosted
                : ReminderCalculations.IsFollowUpDue(daysElapsed, options.Value.FollowUpThresholdDays)
                    ? ReminderType.FollowUp
                    : null;

            if (type is not null)
            {
                candidates.Add((application.Id, application.UserId, type.Value, referenceAt, daysElapsed));
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var candidateApplicationIds = candidates.Select(c => c.ApplicationId).Distinct().ToList();

        var existingKeys = await dbContext.Reminders
            .Where(r => candidateApplicationIds.Contains(r.ApplicationId))
            .Select(r => new { r.ApplicationId, r.Type, r.ReferenceAt })
            .ToListAsync(cancellationToken);

        var existingKeySet = existingKeys
            .Select(k => (k.ApplicationId, k.Type, k.ReferenceAt))
            .ToHashSet();

        var newReminders = candidates
            .Where(c => !existingKeySet.Contains((c.ApplicationId, c.Type, c.ReferenceAt)))
            .Select(c => Reminder.Create(c.UserId, c.ApplicationId, c.Type, c.ReferenceAt, c.DaysElapsed, now))
            .ToList();

        if (newReminders.Count == 0)
        {
            return 0;
        }

        dbContext.Reminders.AddRange(newReminders);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newReminders.Count;
    }
}
