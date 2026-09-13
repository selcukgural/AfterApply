using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
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

    public Task<IReadOnlyList<ReminderResponse>> GetActiveRemindersAsync(Guid userId, CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            ReminderCacheKeys.Active(userId),
            userId,
            async (uid, ct) => (IReadOnlyList<ReminderResponse>)await dbContext.Reminders
                .Where(r => r.UserId == uid && r.DismissedAt == null)
                .Join(dbContext.Applications, r => r.ApplicationId, a => a.Id,
                    (r, a) => new { r, a.CompanyId, a.JobTitle, a.Status })
                // A closed application has nothing left to remind about. Status changes retire
                // reminders as they happen (ApplicationService) and the nightly scan sweeps up the
                // rest; this filter is the guarantee that neither has to be perfect for the list
                // to be right.
                .Where(x => !TerminalApplicationStatuses.Values.Contains(x.Status))
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
        await cache.RemoveAsync(ReminderCacheKeys.Active(userId), cancellationToken);

        return true;
    }

    public async Task<bool> MarkFollowedUpAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == userId && r.DismissedAt == null, cancellationToken);

        if (reminder is null)
        {
            return false;
        }

        // Scoped to the owner as well as to the reminder's own application id: the reminder row
        // already proves ownership, but a read that only matched on id would silently start
        // crossing users the moment anything else ever wrote that column.
        var application = await dbContext.Applications
            .FirstOrDefaultAsync(a => a.Id == reminder.ApplicationId && a.UserId == userId, cancellationToken);

        if (application is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        application.AddEvent(ApplicationEventType.FollowUpSent, now, Source.Manual, metadata: null);
        // Added explicitly, not left to change tracking: Events was never Included, so EF has no
        // prior snapshot of the collection — see ApplicationService.ChangeStatusAsync.
        dbContext.ApplicationEvents.Add(application.Events.Last());
        reminder.Dismiss(now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ReminderCacheKeys.Active(userId), cancellationToken);

        return true;
    }

    public async Task<int> ScanAndGenerateRemindersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var applications = await dbContext.Applications
            .Where(a => !TerminalApplicationStatuses.Values.Contains(a.Status))
            .Select(a => new { a.Id, a.UserId, a.AppliedAt })
            .ToListAsync(cancellationToken);

        // Every open reminder, across users like the application scan above: the sweep that
        // retires reminders is the other half of the job that creates them.
        var activeReminders = await dbContext.Reminders
            .Where(r => r.DismissedAt == null)
            .Select(r => new { r.Id, r.ApplicationId, r.Type })
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(a => a.Id).ToList();

        var historyRows = await dbContext.ApplicationStatusHistories
            .Where(h => applicationIds.Contains(h.ApplicationId))
            .Select(h => new { h.ApplicationId, h.FromStatus, h.ToStatus, h.ChangedAt })
            .ToListAsync(cancellationToken);

        var historyByApplication = historyRows.ToLookup(h => h.ApplicationId);

        var candidates = new List<(Guid ApplicationId, Guid UserId, ReminderType Type, DateTimeOffset ReferenceAt, int DaysElapsed)>();
        var candidateTypeByApplication = new Dictionary<Guid, ReminderType>();
        var beyondHorizon = new HashSet<Guid>();

        foreach (var application in applications)
        {
            var history = historyByApplication[application.Id].ToList();
            var hasResponded = history.Any(h => RespondedStatuses.Contains(h.ToStatus));
            var referenceAt = ReminderCalculations.GetReferenceAt(application.AppliedAt,
                history.Select(h => (h.FromStatus, h.ChangedAt)));
            var daysElapsed = ReminderCalculations.DaysElapsed(referenceAt, now);

            // Past the horizon nothing is created and anything earlier is retired below: the
            // dashboard offers these as one "mark the old batch as ghosted" question instead of
            // one row each (DECISIONS.md 2026-09-13, panel reminders).
            if (ReminderCalculations.IsBeyondHorizon(daysElapsed, options.Value.StaleThresholdDays))
            {
                beyondHorizon.Add(application.Id);
                continue;
            }

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
                candidateTypeByApplication[application.Id] = type.Value;
            }
        }

        // Retire what no longer applies. Three reasons, one UPDATE: the application reached a
        // terminal status (it is absent from the open set — a deleted one is gone through the
        // cascade already), it went past the horizon, or it now rates "possibly ghosted" and the
        // earlier follow-up would otherwise sit beside it as a second row for the same application.
        var openApplicationIds = applicationIds.ToHashSet();
        var retiredIds = activeReminders
            .Where(r => !openApplicationIds.Contains(r.ApplicationId)
                || beyondHorizon.Contains(r.ApplicationId)
                || (r.Type == ReminderType.FollowUp
                    && candidateTypeByApplication.TryGetValue(r.ApplicationId, out var candidateType)
                    && candidateType == ReminderType.PossiblyGhosted))
            .Select(r => r.Id)
            .ToList();

        if (retiredIds.Count > 0)
        {
            await dbContext.Reminders
                .Where(r => retiredIds.Contains(r.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.DismissedAt, now), cancellationToken);
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
