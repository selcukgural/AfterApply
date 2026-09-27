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
    IApplicationService applicationService, TimeProvider? timeProvider = null) : IReminderService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>The reminders that ask the user to write to someone — the ones held back from a
    /// weekend or public holiday. A ghosting question or an interview question asks nothing of
    /// the company, so it appears on the day it is due.</summary>
    private static readonly HashSet<ReminderType> OutreachTypes = [ReminderType.FollowUp, ReminderType.PromiseMissed];

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

    /// <summary>Every reminder the list is allowed to show, before paging: open, not snoozed past
    /// <paramref name="now"/>, the caller's own, and about an application that is still open — see
    /// the terminal filter below.</summary>
    private IQueryable<ReminderResponse> ActiveReminders(Guid userId, DateTimeOffset now)
    {
        return dbContext.Reminders
            .Where(r => r.UserId == userId && r.DismissedAt == null && (r.SnoozedUntil == null || r.SnoozedUntil <= now))
            .Join(dbContext.Applications, r => r.ApplicationId, a => a.Id,
                (r, a) => new { r, a.CompanyId, a.JobTitle, a.Status, a.PromisedReplyBy, a.InterviewAt, a.InterviewStatus })
            // A closed application has nothing left to remind about. Status changes retire
            // reminders as they happen (ApplicationService) and the nightly scan sweeps up the
            // rest; this filter is the guarantee that neither has to be perfect for the list
            // to be right.
            .Where(x => !TerminalApplicationStatuses.Values.Contains(x.Status))
            // An interview question is about the stage it was asked in; a status change made
            // anywhere else has answered it, and the nightly scan retires the row.
            .Where(x => x.r.Type != ReminderType.InterviewHeld || x.InterviewStatus == x.Status)
            .Join(dbContext.Companies, x => x.CompanyId, c => c.Id,
                (x, c) => new { x.r, x.JobTitle, x.PromisedReplyBy, x.Status, x.InterviewAt, CompanyName = c.Name })
            // An interview question first: it is about yesterday, and its answer is only easy while
            // the interview is fresh. Then the application that has waited longest, and among
            // equals the reminder created first: the one that actually needs attention is on page
            // one, not the freshest nudge. Ordered here rather than on the client because the
            // client only ever sees one page.
            .OrderByDescending(x => x.r.Type == ReminderType.InterviewHeld)
            .ThenByDescending(x => x.r.DaysElapsedAtCreation)
            .ThenBy(x => x.r.CreatedAt)
            .ThenBy(x => x.r.Id)
            .Select(x => new ReminderResponse(
                x.r.Id, x.r.ApplicationId, x.CompanyName, x.JobTitle, x.r.Type, x.r.DaysElapsedAtCreation, x.r.CreatedAt,
                null,
                x.r.Type == ReminderType.PromiseMissed ? x.PromisedReplyBy : null,
                x.Status,
                x.r.Type == ReminderType.InterviewHeld ? x.InterviewAt : null,
                x.r.DeferredFor,
                x.r.DeferredFor == null ? null : x.r.SnoozedUntil));
    }

    public Task<PagedResult<ReminderResponse>> GetActiveRemindersAsync(Guid userId, GetRemindersQuery query,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            CacheKeys.Reminders.ActivePage(userId, query.Page, query.PageSize),
            (userId, query),
            async (state, ct) =>
            {
                var active = ActiveReminders(state.userId, _timeProvider.GetUtcNow());
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
                    var median = await UserResponseMedian.GetAsync(dbContext, state.userId, ct);
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

    public async Task<bool> DismissAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == userId, cancellationToken);

        if (reminder is null)
        {
            return false;
        }

        reminder.Dismiss(_timeProvider.GetUtcNow());
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

        var now = _timeProvider.GetUtcNow();
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

        var actualCount = await ActiveReminders(userId, _timeProvider.GetUtcNow()).CountAsync(cancellationToken);
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
        var now = _timeProvider.GetUtcNow();
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

        var now = _timeProvider.GetUtcNow();
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

    public async Task<bool> SnoozeAsync(Guid userId, Guid reminderId, SnoozeReminderRequest request,
        CancellationToken cancellationToken)
    {
        var until = _timeProvider.GetUtcNow().AddDays(request.Days);
        var affected = await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId && r.DismissedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.SnoozedUntil, until)
                .SetProperty(r => r.DeferredFor, (ReminderDeferral?)null), cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);
        return true;
    }

    public async Task<bool> UnsnoozeAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var affected = await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.SnoozedUntil, (DateTimeOffset?)null)
                .SetProperty(r => r.DeferredFor, (ReminderDeferral?)null), cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);
        return true;
    }

    public async Task<InterviewOutcomeResponse?> AnswerInterviewAsync(Guid userId, Guid reminderId,
        InterviewOutcomeRequest request, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId && r.DismissedAt == null)
            .Select(r => new { r.ApplicationId, r.Type })
            .FirstOrDefaultAsync(cancellationToken);

        if (reminder is null)
        {
            return null;
        }

        if (reminder.Type != ReminderType.InterviewHeld)
        {
            throw new ReminderNotAnInterviewException();
        }

        var fromStatus = await dbContext.Applications
            .Where(a => a.Id == reminder.ApplicationId && a.UserId == userId)
            .Select(a => (ApplicationStatus?)a.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (fromStatus is null)
        {
            return null;
        }

        // Every answer goes through the application service, the one place a status or a reply
        // date is written: the history row, the board, the reminder sweep behind a terminal status
        // all happen exactly as they do for the same change made on the application page.
        InterviewOutcomeResponse outcome;
        switch (request.Outcome)
        {
            case InterviewOutcome.NextStage:
                await applicationService.ChangeStatusAsync(userId, reminder.ApplicationId,
                    new ChangeStatusRequest(request.NextStatus!.Value, Note: null, ChangedAt: null), cancellationToken);
                outcome = new InterviewOutcomeResponse(fromStatus, request.NextStatus, null);
                break;
            case InterviewOutcome.Rejected:
                await applicationService.ChangeStatusAsync(userId, reminder.ApplicationId,
                    new ChangeStatusRequest(ApplicationStatus.Rejected, Note: null, ChangedAt: null), cancellationToken);
                outcome = new InterviewOutcomeResponse(fromStatus, ApplicationStatus.Rejected, null);
                break;
            default:
                if (request.PromisedReplyBy is not null)
                {
                    await applicationService.SetReplyPromiseAsync(userId, reminder.ApplicationId,
                        new SetReplyPromiseRequest(request.PromisedReplyBy), cancellationToken);
                }

                outcome = new InterviewOutcomeResponse(null, null, request.PromisedReplyBy);
                break;
        }

        // Closed here whatever the answer: a status change to a stage that is not terminal leaves
        // the application's reminders alone, and "they did not say when" changes nothing at all.
        await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.DismissedAt, _timeProvider.GetUtcNow()), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);

        return outcome;
    }

    public async Task<bool> UndoInterviewAnswerAsync(Guid userId, Guid reminderId, UndoInterviewOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId && r.Type == ReminderType.InterviewHeld)
            .Select(r => new { r.ApplicationId })
            .FirstOrDefaultAsync(cancellationToken);

        if (reminder is null)
        {
            return false;
        }

        if (request is { FromStatus: { } revertTo, ToStatus: { } expected })
        {
            // The same compare-and-set as every other undo: a status the user has since moved on
            // by hand keeps the newer decision.
            await applicationService.UndoBulkStatusAsync(userId,
                new UndoBulkStatusRequest([new UndoBulkStatusEntry(reminder.ApplicationId, expected, revertTo)]),
                cancellationToken);
        }

        if (request.PromisedReplyBy is { } promised)
        {
            var stillThatPromise = await dbContext.Applications
                .AnyAsync(a => a.Id == reminder.ApplicationId && a.UserId == userId && a.PromisedReplyBy == promised,
                    cancellationToken);
            if (stillThatPromise)
            {
                await applicationService.SetReplyPromiseAsync(userId, reminder.ApplicationId,
                    new SetReplyPromiseRequest(null), cancellationToken);
            }
        }

        await dbContext.Reminders
            .Where(r => r.Id == reminderId && r.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.DismissedAt, (DateTimeOffset?)null)
                .SetProperty(r => r.SnoozedUntil, (DateTimeOffset?)null)
                .SetProperty(r => r.DeferredFor, (ReminderDeferral?)null), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<UpcomingInterviewResponse>> GetUpcomingInterviewsAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var from = now - ReminderCalculations.InterviewDuration;
        var to = now.AddDays(UpcomingInterviewDays);

        return await dbContext.Applications
            .Where(a => a.UserId == userId && a.InterviewAt != null && a.InterviewStatus == a.Status
                        && a.InterviewAt >= from && a.InterviewAt <= to)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id,
                (a, c) => new { a.Id, CompanyName = c.Name, a.JobTitle, a.Status, a.InterviewAt, a.InterviewFormat })
            .OrderBy(x => x.InterviewAt)
            .Take(MaxUpcomingInterviews)
            .Select(x => new UpcomingInterviewResponse(x.Id, x.CompanyName, x.JobTitle, x.Status,
                x.InterviewAt!.Value, x.InterviewFormat ?? InterviewFormat.Online))
            .ToListAsync(cancellationToken);
    }

    /// <summary>How far ahead the card looks: two weeks covers every interview a candidate would
    /// still want in front of them, and nothing so far out that it is noise.</summary>
    private const int UpcomingInterviewDays = 14;

    private const int MaxUpcomingInterviews = 10;

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

        var now = _timeProvider.GetUtcNow();
        if (now < pause.RemindersPausedUntil)
        {
            return new ReminderPauseResponse(ReminderPauseState.Paused, pause.RemindersPausedFrom, pause.RemindersPausedUntil, 0);
        }

        var silenced = await SilencedDuringPause(userId, pause.RemindersPausedFrom.Value).CountAsync(cancellationToken);
        return new ReminderPauseResponse(ReminderPauseState.Returned, pause.RemindersPausedFrom, pause.RemindersPausedUntil, silenced);
    }

    public async Task<ReminderPauseResponse> PauseAsync(Guid userId, PauseRemindersRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
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
        var now = _timeProvider.GetUtcNow();
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
        var now = _timeProvider.GetUtcNow();
        var created = await ScanRemindersAsync(now, cancellationToken);
        // After the reminders, so a "how did it go?" row created in this very run rings the bell.
        await WriteInterviewNotificationsAsync(now, cancellationToken);
        return created;
    }

    /// <summary>How far ahead an interview is announced in the bell. The scan runs once a night (early
    /// morning in Türkiye), so a day and a half catches today's interviews and tomorrow's.</summary>
    private static readonly TimeSpan UpcomingNotificationWindow = TimeSpan.FromHours(36);

    /// <summary>
    /// The bell's two interview rows: "coming up" for an interview inside
    /// <see cref="UpcomingNotificationWindow"/>, and "how did it go?" for every open InterviewHeld
    /// reminder. One row per interview per kind (the unique index is the backstop), and none at all
    /// for a user who switched interview notifications off — not written, rather than hidden.
    /// </summary>
    private async Task WriteInterviewNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var horizon = now + UpcomingNotificationWindow;
        var upcoming = await dbContext.Applications
            .Where(a => a.InterviewAt != null && a.InterviewStatus == a.Status && a.InterviewAt > now && a.InterviewAt <= horizon)
            .Where(a => dbContext.Users.Any(u => u.Id == a.UserId && u.NotifyInterviews))
            .Select(a => new { a.UserId, ApplicationId = a.Id, InterviewAt = a.InterviewAt!.Value, Kind = InterviewNotificationKind.Upcoming })
            .ToListAsync(cancellationToken);

        var held = await dbContext.Reminders
            .Where(r => r.Type == ReminderType.InterviewHeld && r.DismissedAt == null)
            .Where(r => dbContext.Users.Any(u => u.Id == r.UserId && u.NotifyInterviews))
            .Select(r => new { r.UserId, r.ApplicationId, InterviewAt = r.ReferenceAt, Kind = InterviewNotificationKind.Held })
            .ToListAsync(cancellationToken);

        var due = upcoming.Concat(held).ToList();
        if (due.Count == 0)
        {
            return;
        }

        var applicationIds = due.Select(d => d.ApplicationId).Distinct().ToList();
        var existing = (await dbContext.InterviewNotifications
                .Where(n => applicationIds.Contains(n.ApplicationId))
                .Select(n => new { n.ApplicationId, n.Kind, n.InterviewAt })
                .ToListAsync(cancellationToken))
            .Select(n => (n.ApplicationId, n.Kind, n.InterviewAt))
            .ToHashSet();

        var fresh = due
            .Where(d => !existing.Contains((d.ApplicationId, d.Kind, d.InterviewAt)))
            .Select(d => InterviewNotification.Create(d.UserId, d.ApplicationId, d.Kind, d.InterviewAt, now))
            .ToList();

        if (fresh.Count > 0)
        {
            dbContext.InterviewNotifications.AddRange(fresh);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<int> ScanRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {

        var applications = await dbContext.Applications
            .Where(a => !TerminalApplicationStatuses.Values.Contains(a.Status))
            .Select(a => new
            {
                a.Id, a.UserId, a.AppliedAt, a.Status, a.PromisedReplyBy, a.PromisedReplySince, a.InterviewAt, a.InterviewStatus,
                CompanyCountry = dbContext.Companies.Where(c => c.Id == a.CompanyId).Select(c => c.Country).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Every open reminder, across users like the application scan above: the sweep that
        // retires reminders is the other half of the job that creates them.
        var activeReminders = await dbContext.Reminders
            .Where(r => r.DismissedAt == null)
            .Select(r => new { r.Id, r.UserId, r.ApplicationId, r.Type, r.ReferenceAt })
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
        // Applications whose company promised a date that has not come yet: nothing is due, and
        // any reminder already open about them is retired below.
        var waitingOnPromise = new HashSet<Guid>();

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

            // A promise still open on this stage decides first: before its date nothing is due,
            // after it the follow-up becomes "the date has passed". Only an Overdue promise is open
            // — Kept/Late/Void ones were answered by a later status change, which also moved the
            // reference date the rest of this loop measures from.
            var promise = application is { PromisedReplyBy: { } promisedBy, PromisedReplySince: { } promisedSince }
                ? ReplyPromises.Evaluate(promisedBy, promisedSince, history.Select(h => (h.ToStatus, h.ChangedAt)), now)
                : null;
            if (promise?.Outcome == ReplyPromiseOutcome.Pending)
            {
                waitingOnPromise.Add(application.Id);
                continue;
            }

            // An interview of this stage that is over and has not been answered asks "how did it
            // go?" in place of any follow-up. Only while no reply date is recorded: a promise is
            // itself an answer, and the promise rules above and below govern from then on.
            if (promise is null
                && application is { InterviewAt: { } interviewAt } && application.InterviewStatus == application.Status
                && ReminderCalculations.IsInterviewQuestionDue(interviewAt, now, options.Value.FollowUpThresholdDays))
            {
                candidates.Add((application.Id, application.UserId, ReminderType.InterviewHeld, interviewAt,
                    ReminderCalculations.DaysElapsed(interviewAt, now)));
                candidateTypeByApplication[application.Id] = ReminderType.InterviewHeld;
                continue;
            }

            // Ghosting takes precedence: an application eligible for both never
            // surfaces both suggestions at once (product decision, Sprint 6 plan).
            if (ReminderCalculations.IsPossiblyGhosted(hasResponded, daysElapsed, options.Value.GhostingThresholdDays))
            {
                candidates.Add((application.Id, application.UserId, ReminderType.PossiblyGhosted, referenceAt, daysElapsed));
                candidateTypeByApplication[application.Id] = ReminderType.PossiblyGhosted;
            }
            else if (promise?.Outcome == ReplyPromiseOutcome.Overdue)
            {
                var promisedAt = PromisedAt(application.PromisedReplyBy!.Value);
                candidates.Add((application.Id, application.UserId, ReminderType.PromiseMissed, promisedAt,
                    ReminderCalculations.DaysElapsed(promisedAt, now)));
                candidateTypeByApplication[application.Id] = ReminderType.PromiseMissed;
            }
            else if (ReminderCalculations.IsFollowUpDue(daysElapsed, options.Value.FollowUpThresholdDays))
            {
                candidates.Add((application.Id, application.UserId, ReminderType.FollowUp, referenceAt, daysElapsed));
                candidateTypeByApplication[application.Id] = ReminderType.FollowUp;
            }
        }

        // Retire what no longer applies, in one UPDATE: the application reached a terminal status
        // (it is absent from the open set — a deleted one is gone through the cascade already), it
        // went past the horizon, the company's promised date has not come yet, or a stronger
        // reminder now stands for it and the earlier one would sit beside it as a second row —
        // "possibly ghosted" over a follow-up or a missed promise, a missed promise over a
        // follow-up. A missed-promise row whose date is no longer the promised one (moved,
        // cleared, answered) goes too.
        var openApplicationIds = applicationIds.ToHashSet();
        var promisedAtByApplication = applications
            .Where(a => a.PromisedReplyBy is not null)
            .ToDictionary(a => a.Id, a => PromisedAt(a.PromisedReplyBy!.Value));
        var interviewAtByApplication = applications
            .Where(a => a.InterviewAt is not null)
            .ToDictionary(a => a.Id, a => a.InterviewAt!.Value);
        var retired = activeReminders
            .Where(r => !openApplicationIds.Contains(r.ApplicationId)
                || beyondHorizon.Contains(r.ApplicationId)
                || waitingOnPromise.Contains(r.ApplicationId)
                || (candidateTypeByApplication.TryGetValue(r.ApplicationId, out var candidateType)
                    && Outranks(candidateType, r.Type))
                || (r.Type == ReminderType.PromiseMissed
                    && (candidateTypeByApplication.GetValueOrDefault(r.ApplicationId) != ReminderType.PromiseMissed
                        || promisedAtByApplication.GetValueOrDefault(r.ApplicationId) != r.ReferenceAt))
                // An interview question whose interview is no longer the one due a question — the
                // window closed, the date moved, the stage changed — goes the same way.
                || (r.Type == ReminderType.InterviewHeld
                    && (candidateTypeByApplication.GetValueOrDefault(r.ApplicationId) != ReminderType.InterviewHeld
                        || interviewAtByApplication.GetValueOrDefault(r.ApplicationId) != r.ReferenceAt)))
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

        var countryByApplication = applications.ToDictionary(a => a.Id, a => a.CompanyCountry);
        var newReminders = candidates
            .Where(c => !existingKeySet.Contains((c.ApplicationId, c.Type, c.ReferenceAt)))
            .Select(c =>
            {
                var reminder = Reminder.Create(c.UserId, c.ApplicationId, c.Type, c.ReferenceAt, c.DaysElapsed, now);
                // Due on a weekend or public holiday: held back to the next working morning, and
                // the row says why (BusinessCalendar).
                if (options.Value.HoldOutreachOnDaysOff
                    && OutreachTypes.Contains(c.Type)
                    && BusinessCalendar.DeferralFor(now, BusinessCalendar.UsesTurkishHolidays(countryByApplication.GetValueOrDefault(c.ApplicationId)))
                        is { } deferral)
                {
                    reminder.Defer(deferral.Until, deferral.Reason);
                }

                return reminder;
            })
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

    /// <summary>The promised date as the instant a PromiseMissed reminder is keyed and counted from.</summary>
    private static DateTimeOffset PromisedAt(DateOnly promisedBy) =>
        new(promisedBy.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>Whether a reminder of <paramref name="candidate"/> type replaces an open one of
    /// <paramref name="open"/> type on the same application.</summary>
    private static bool Outranks(ReminderType candidate, ReminderType open) => (candidate, open) switch
    {
        (ReminderType.PossiblyGhosted, ReminderType.FollowUp or ReminderType.PromiseMissed) => true,
        (ReminderType.PromiseMissed, ReminderType.FollowUp) => true,
        (ReminderType.InterviewHeld, ReminderType.FollowUp) => true,
        _ => false
    };

    private async Task EvictActiveListsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            await cache.RemoveByTagAsync(CacheKeys.Reminders.ActiveTag(userId), cancellationToken);
        }
    }
}

public sealed class ReminderNotAnInterviewException()
    : DomainException("REMINDER_NOT_AN_INTERVIEW", "This reminder is not about an interview.");
