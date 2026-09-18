using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Notifications;

internal sealed class ReminderService(AppDbContext dbContext, IOptions<NotificationOptions> options, HybridCache cache,
    IApplicationService applicationService) : IReminderService
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

    /// <summary>Every reminder the list is allowed to show, before paging: open, the caller's own,
    /// and about an application that is still open — see the terminal filter below.</summary>
    private IQueryable<ReminderResponse> ActiveReminders(Guid userId)
    {
        return dbContext.Reminders
            .Where(r => r.UserId == userId && r.DismissedAt == null)
            .Join(dbContext.Applications, r => r.ApplicationId, a => a.Id,
                (r, a) => new { r, a.CompanyId, a.JobTitle, a.Status })
            // A closed application has nothing left to remind about. Status changes retire
            // reminders as they happen (ApplicationService) and the nightly scan sweeps up the
            // rest; this filter is the guarantee that neither has to be perfect for the list
            // to be right.
            .Where(x => !TerminalApplicationStatuses.Values.Contains(x.Status))
            .Join(dbContext.Companies, x => x.CompanyId, c => c.Id,
                (x, c) => new { x.r, x.JobTitle, CompanyName = c.Name })
            // The application that has waited longest first, and among equals the reminder
            // created first: the one that actually needs attention is on page one, not the
            // freshest nudge. Ordered here rather than on the client because the client only
            // ever sees one page.
            .OrderByDescending(x => x.r.DaysElapsedAtCreation)
            .ThenBy(x => x.r.CreatedAt)
            .ThenBy(x => x.r.Id)
            .Select(x => new ReminderResponse(
                x.r.Id, x.r.ApplicationId, x.CompanyName, x.JobTitle, x.r.Type, x.r.DaysElapsedAtCreation, x.r.CreatedAt));
    }

    public Task<PagedResult<ReminderResponse>> GetActiveRemindersAsync(Guid userId, GetRemindersQuery query,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            CacheKeys.Reminders.ActivePage(userId, query.Page, query.PageSize),
            (userId, query),
            async (state, ct) =>
            {
                var active = ActiveReminders(state.userId);
                var totalCount = await active.CountAsync(ct);
                var items = await active
                    .Skip((state.query.Page - 1) * state.query.PageSize)
                    .Take(state.query.PageSize)
                    .ToListAsync(ct);

                // The user's own norm, once per page rather than once per row: every row on the
                // page reads against the same median, and a page with no "possibly ghosted" row
                // has no sentence to put it in.
                if (items.Any(i => i.Type == ReminderType.PossiblyGhosted))
                {
                    var median = await GetUserMedianResponseDaysAsync(state.userId, ct);
                    if (median is not null)
                    {
                        items = items.Select(i => i with { UserMedianResponseDays = median }).ToList();
                    }
                }

                return new PagedResult<ReminderResponse>(items, totalCount, state.query.Page, state.query.PageSize);
            },
            ActiveRemindersCacheOptions,
            tags: [CacheKeys.Reminders.ActiveTag(userId)],
            cancellationToken: cancellationToken).AsTask();
    }

    /// <summary>
    /// Days from AppliedAt to the first "responded" status transition, per answered application,
    /// reduced to the median by <see cref="ReminderCalculations.UserMedianResponseDays"/>. The same
    /// definition of "first reply" as AnalyticsService.GetOverviewAsync, computed in SQL here
    /// because this runs on every reminders page load and only needs one number back.
    /// </summary>
    private async Task<int?> GetUserMedianResponseDaysAsync(Guid userId, CancellationToken cancellationToken)
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
        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);

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
        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);

        return true;
    }

    /// <summary>
    /// Narrows to exactly the open reminders a bulk selection covers, always the caller's own. An
    /// id list is intersected with the user's rows rather than trusted — an id in a request body is
    /// a claim, never proof — so a foreign id matches nothing instead of leaking that it exists.
    /// The terminal-application filter is deliberately absent here: a reminder the list would hide
    /// is still the user's to close, and closing it is the harmless direction.
    /// </summary>
    private IQueryable<Reminder> ResolveSelection(Guid userId, ReminderSelection selection)
    {
        var open = dbContext.Reminders.Where(r => r.UserId == userId && r.DismissedAt == null);
        return selection.Ids is { Count: > 0 } ids ? open.Where(r => ids.Contains(r.Id)) : open;
    }

    /// <summary>
    /// Refuses the operation when the number of open reminders is not what the user was shown. Only
    /// meaningful for an "all" selection: an id list is already the exact set the user ticked, and
    /// comparing it against itself would reject a legitimate request whose rows another tab just
    /// answered. Compared against what the list shows — open reminders of open applications —
    /// because that is the number the card printed next to "select all".
    /// </summary>
    private async Task GuardExpectedCountAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken)
    {
        if (!request.Selection.All || request.ExpectedCount is null)
        {
            return;
        }

        var actualCount = await ActiveReminders(userId).CountAsync(cancellationToken);
        if (actualCount != request.ExpectedCount.Value)
        {
            throw new BulkCountMismatchException(request.ExpectedCount.Value, actualCount);
        }
    }

    public async Task<BulkReminderResponse> BulkDismissAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken)
    {
        await GuardExpectedCountAsync(userId, request, cancellationToken);

        // Set-based: nothing is loaded, so the size of an "all" selection costs one UPDATE
        // however many rows it covers.
        var now = DateTimeOffset.UtcNow;
        var affected = await ResolveSelection(userId, request.Selection)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.DismissedAt, now), cancellationToken);

        if (affected > 0)
        {
            await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);
        }

        return new BulkReminderResponse(affected);
    }

    public async Task<BulkReminderResponse> BulkMarkFollowedUpAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken)
    {
        await GuardExpectedCountAsync(userId, request, cancellationToken);

        var reminders = await ResolveSelection(userId, request.Selection).ToListAsync(cancellationToken);
        if (reminders.Count == 0)
        {
            return new BulkReminderResponse(0);
        }

        // One FollowUpSent per application, not per reminder: a follow-up is something the user did
        // once, and two reminders about the same application do not make it two.
        var applicationIds = reminders.Select(r => r.ApplicationId).Distinct().ToList();
        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId && applicationIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var application in applications)
        {
            application.AddEvent(ApplicationEventType.FollowUpSent, now, Source.Manual, metadata: null);
            // Added explicitly for the reason MarkFollowedUpAsync gives: Events was never Included.
            dbContext.ApplicationEvents.Add(application.Events.Last());
        }

        foreach (var reminder in reminders)
        {
            reminder.Dismiss(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);

        return new BulkReminderResponse(reminders.Count);
    }

    public async Task<BulkChangeStatusResponse> BulkMarkGhostedAsync(Guid userId, BulkReminderRequest request, CancellationToken cancellationToken)
    {
        await GuardExpectedCountAsync(userId, request, cancellationToken);

        var applicationIds = await ResolveSelection(userId, request.Selection)
            .Select(r => r.ApplicationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // The status change is the answer; the application service closes the reminders behind it
        // (RetireRemindersAsync) exactly as it does for a single "mark as ghosted" on the row.
        return await applicationService.GhostApplicationsAsync(userId, applicationIds, cancellationToken);
    }

    public async Task<ReminderPauseResponse> GetPauseAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pause = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.RemindersPausedFrom, u.RemindersPausedUntil })
            .FirstOrDefaultAsync(cancellationToken);

        if (pause?.RemindersPausedUntil is null || pause.RemindersPausedFrom is null)
        {
            return new ReminderPauseResponse(ReminderPauseState.None, null, null, 0);
        }

        var now = DateTimeOffset.UtcNow;
        if (now < pause.RemindersPausedUntil)
        {
            return new ReminderPauseResponse(ReminderPauseState.Paused, pause.RemindersPausedFrom, pause.RemindersPausedUntil, 0);
        }

        var silenced = await SilencedDuringPause(userId, pause.RemindersPausedFrom.Value).CountAsync(cancellationToken);
        return new ReminderPauseResponse(ReminderPauseState.Returned, pause.RemindersPausedFrom, pause.RemindersPausedUntil, silenced);
    }

    public async Task<ReminderPauseResponse> PauseAsync(Guid userId, PauseRemindersRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var until = now.AddDays(request.Days);
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.RemindersPausedFrom, now)
                .SetProperty(u => u.RemindersPausedUntil, until), cancellationToken);

        return new ReminderPauseResponse(ReminderPauseState.Paused, now, until, 0);
    }

    public async Task<ReminderPauseResponse> EndPauseAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // Only a running break is cut short; an ended one keeps its real end date so the return
        // question still describes the break the user actually took.
        await dbContext.Users
            .Where(u => u.Id == userId && u.RemindersPausedUntil > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.RemindersPausedUntil, now), cancellationToken);

        return await GetPauseAsync(userId, cancellationToken);
    }

    public async Task AcknowledgePauseAsync(Guid userId, CancellationToken cancellationToken)
    {
        await ClearPauseAsync(userId, cancellationToken);
    }

    public async Task<BulkChangeStatusResponse> CloseSilencedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pausedFrom = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.RemindersPausedFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (pausedFrom is null)
        {
            return new BulkChangeStatusResponse(0, 0, []);
        }

        var applicationIds = await SilencedDuringPause(userId, pausedFrom.Value)
            .Select(r => r.ApplicationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Same act as the card's bulk "mark as ghosted": the status change is the answer, the
        // application service retires the reminders behind it, and the response carries what it
        // moved so the strip's undo can put it back.
        var result = await applicationService.GhostApplicationsAsync(userId, applicationIds, cancellationToken);
        await ClearPauseAsync(userId, cancellationToken);
        return result;
    }

    /// <summary>The open "possibly ghosted" reminders raised since the break began. Follow-up
    /// reminders are excluded on purpose: an application that replied and then went quiet is a
    /// conversation to pick up, not one to close.</summary>
    private IQueryable<Reminder> SilencedDuringPause(Guid userId, DateTimeOffset pausedFrom)
    {
        return dbContext.Reminders
            .Where(r => r.UserId == userId && r.DismissedAt == null
                        && r.Type == ReminderType.PossiblyGhosted && r.CreatedAt >= pausedFrom);
    }

    private async Task ClearPauseAsync(Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.RemindersPausedFrom, (DateTimeOffset?)null)
                .SetProperty(u => u.RemindersPausedUntil, (DateTimeOffset?)null), cancellationToken);
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
            .Select(r => new { r.Id, r.UserId, r.ApplicationId, r.Type })
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
        var retired = activeReminders
            .Where(r => !openApplicationIds.Contains(r.ApplicationId)
                || beyondHorizon.Contains(r.ApplicationId)
                || (r.Type == ReminderType.FollowUp
                    && candidateTypeByApplication.TryGetValue(r.ApplicationId, out var candidateType)
                    && candidateType == ReminderType.PossiblyGhosted))
            .ToList();

        if (retired.Count > 0)
        {
            var retiredIds = retired.Select(r => r.Id).ToList();
            await dbContext.Reminders
                .Where(r => retiredIds.Contains(r.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.DismissedAt, now), cancellationToken);

            // This job writes reminders without going through the per-answer paths that evict the
            // list, so it evicts by hand: a row retired here must not linger on a dashboard whose
            // cached page still lists it. Before the early return below, which skips the create
            // half but not this one.
            await EvictActiveListsAsync(retired.Select(r => r.UserId), cancellationToken);
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
        await EvictActiveListsAsync(newReminders.Select(r => r.UserId), cancellationToken);

        return newReminders.Count;
    }

    private async Task EvictActiveListsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);
        }
    }
}
