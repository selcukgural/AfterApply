using AfterApply.Domain.Applications;
using AfterApply.Domain.Board;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Board;

/// <summary>
/// Keeps a user's board in step with changes made elsewhere — a new application, a saved posting,
/// an e-mail moving a status. Every method only stages changes on the shared context; the caller's
/// own SaveChanges writes them together with the change that caused them, so the board can never
/// show a move that did not happen. What each event does is <see cref="BoardAutoPlacement"/>'s
/// table; this class only applies it.
///
/// Nothing happens for a user whose board has never been opened (no <see cref="BoardState"/>):
/// the first opening picks the same recent applications up anyway.
/// </summary>
internal sealed class BoardSync(AppDbContext dbContext, IOptions<BoardOptions> options, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        options.Value.Enabled
            ? dbContext.BoardStates.AnyAsync(s => s.UserId == userId, cancellationToken)
            : Task.FromResult(false);

    /// <summary>A new application, already added to the context. Imports put nothing on the board.</summary>
    public async Task OnApplicationCreatedAsync(DomainApplication application, CancellationToken cancellationToken)
    {
        if (BoardAutoPlacement.ForNewApplication(application.Source) is not { } origin
            || !await IsActiveAsync(application.UserId, cancellationToken))
        {
            return;
        }

        var column = BoardColumns.For(application.Status);
        var position = BoardPositions.Top(await BoardQueries.TopOfAsync(dbContext, application.UserId, column, cancellationToken));
        dbContext.BoardCards.Add(BoardCard.ForApplication(application.UserId, application.Id,
            column == BoardColumn.Closed, position, origin, _timeProvider.GetUtcNow()));
    }

    /// <summary>A new saved posting, already added to the context.</summary>
    public async Task OnTrackedJobCreatedAsync(TrackedJob trackedJob, BoardCardOrigin origin, CancellationToken cancellationToken)
    {
        if (!await IsActiveAsync(trackedJob.UserId, cancellationToken))
        {
            return;
        }

        var position = BoardPositions.Top(await BoardQueries.TopOfAsync(dbContext, trackedJob.UserId, BoardColumn.Saved, cancellationToken));
        dbContext.BoardCards.Add(BoardCard.ForTrackedJob(trackedJob.UserId, trackedJob.Id, position, origin,
            _timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// A saved posting became <paramref name="application"/> (both already staged: the application
    /// added, the posting removed). A posting on the board keeps its card, now on top of
    /// "Applied"; one that was not is treated as any new application.
    /// </summary>
    public async Task OnTrackedJobConvertedAsync(TrackedJob trackedJob, DomainApplication application,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveAsync(application.UserId, cancellationToken))
        {
            return;
        }

        var card = await dbContext.BoardCards
            .FirstOrDefaultAsync(c => c.UserId == application.UserId && c.TrackedJobId == trackedJob.Id, cancellationToken);
        if (card is null)
        {
            await OnApplicationCreatedAsync(application, cancellationToken);
            return;
        }

        var column = BoardColumns.For(application.Status);
        card.BecomeApplication(application.Id,
            BoardPositions.Top(await BoardQueries.TopOfAsync(dbContext, application.UserId, column, cancellationToken)));
        card.SyncClosed(column == BoardColumn.Closed, _timeProvider.GetUtcNow());
        if (BoardAutoPlacement.ForNewApplication(application.Source) is BoardCardOrigin.Extension)
        {
            card.MarkArrived(BoardCardOrigin.Extension);
        }
    }

    /// <summary>
    /// Status changes already applied to the applications (not yet saved), all from one act —
    /// hence one origin. Cards on the board follow their rule; an e-mail about progress brings a
    /// card the user had taken off back.
    /// </summary>
    public async Task OnStatusChangedAsync(Guid userId, IReadOnlyCollection<(Guid ApplicationId, ApplicationStatus NewStatus)> changes,
        StatusChangeOrigin origin, CancellationToken cancellationToken)
    {
        if (changes.Count == 0 || !await IsActiveAsync(userId, cancellationToken))
        {
            return;
        }

        var ids = changes.Select(c => (Guid?)c.ApplicationId).ToList();
        var cards = await dbContext.BoardCards
            .Where(c => c.UserId == userId && ids.Contains(c.ApplicationId))
            .ToDictionaryAsync(c => c.ApplicationId!.Value, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        // Each card lifted in this act goes above the previous one, so the column's top is read
        // once and then walked upwards in memory.
        var tops = new Dictionary<BoardColumn, long?>();

        async Task<long> NextTopAsync(BoardColumn column)
        {
            if (!tops.TryGetValue(column, out var top))
            {
                top = await BoardQueries.TopOfAsync(dbContext, userId, column, cancellationToken);
            }

            var next = BoardPositions.Top(top);
            tops[column] = next;
            return next;
        }

        foreach (var (applicationId, newStatus) in changes)
        {
            var column = BoardColumns.For(newStatus);
            var isClosed = column == BoardColumn.Closed;
            var onBoard = cards.TryGetValue(applicationId, out var card);

            switch (BoardAutoPlacement.ForStatusChange(onBoard, newStatus, origin))
            {
                case BoardPlacementAction.ReturnFromEmail:
                    dbContext.BoardCards.Add(BoardCard.ForApplication(userId, applicationId, isClosed,
                        await NextTopAsync(column), BoardCardOrigin.EmailReturned, now));
                    continue;
                case BoardPlacementAction.MoveToTop:
                    card!.MoveTo(await NextTopAsync(column));
                    break;
                case BoardPlacementAction.MoveToTopFromEmail:
                    card!.MoveTo(await NextTopAsync(column));
                    card.MarkArrived(BoardCardOrigin.Email);
                    break;
            }

            card?.SyncClosed(isClosed, now);
        }
    }
}
