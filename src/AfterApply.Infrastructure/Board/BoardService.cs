using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Board;
using AfterApply.Application.Board.Contracts;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Board;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Board;

internal sealed class BoardService(
    AppDbContext dbContext,
    IApplicationService applicationService,
    ITrackedJobService trackedJobService,
    IOptions<BoardOptions> options,
    TimeProvider? timeProvider = null) : IBoardService, IBoardMaintenanceService
{
    private static readonly BoardColumn[] Columns =
        [BoardColumn.Saved, BoardColumn.Applied, BoardColumn.InProgress, BoardColumn.Offer, BoardColumn.Closed];

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<BoardResponse> GetBoardAsync(Guid userId, BoardFilterQuery query, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(userId, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var pages = new List<BoardColumnPage>(Columns.Length);
        foreach (var column in Columns)
        {
            pages.Add(await ReadColumnAsync(userId, column, query, cursor: null, now, cancellationToken));
        }

        var closedCutoff = ClosedCutoff(now);
        var unseen = await dbContext.BoardCards
            .CountAsync(c => c.UserId == userId && c.SeenAt == null && (c.ClosedAt == null || c.ClosedAt >= closedCutoff),
                cancellationToken);

        return new BoardResponse(pages, unseen, options.Value.ClosedVisibleDays, options.Value.SilentDays);
    }

    public async Task<BoardColumnPage> GetColumnAsync(Guid userId, BoardColumn column, BoardColumnQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(userId, cancellationToken);

        // The validator already refused a cursor that does not decode; a null here is the first page.
        BoardCursor? cursor = BoardCursor.TryDecode(query.Cursor, out var decoded) ? decoded : null;
        return await ReadColumnAsync(userId, column, query, cursor, _timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<AddBoardCardsResponse> AddAsync(Guid userId, AddBoardCardsRequest request, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(userId, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockBoardAsync(userId, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var requestedApplications = (request.ApplicationIds ?? []).Distinct().ToList();
        var requestedPostings = (request.TrackedJobIds ?? []).Distinct().ToList();

        // Ids from the body are claims: only the caller's own rows, not yet on the board, survive.
        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId && requestedApplications.Contains(a.Id)
                && !dbContext.BoardCards.Any(c => c.ApplicationId == a.Id))
            .Select(a => new { a.Id, a.Status })
            .ToDictionaryAsync(a => a.Id, a => a.Status, cancellationToken);
        var postings = await dbContext.TrackedJobs
            .Where(t => t.UserId == userId && requestedPostings.Contains(t.Id)
                && !dbContext.BoardCards.Any(c => c.TrackedJobId == t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

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

        // Walked backwards so the first id of the request ends up highest — the order the user
        // ticked them in is the order they read them in.
        foreach (var id in Enumerable.Reverse(requestedApplications))
        {
            if (!applications.TryGetValue(id, out var status))
            {
                continue;
            }

            var column = BoardColumns.For(status);
            dbContext.BoardCards.Add(BoardCard.ForApplication(userId, id, column == BoardColumn.Closed,
                await NextTopAsync(column), BoardCardOrigin.Manual, now));
        }

        foreach (var id in Enumerable.Reverse(requestedPostings).Where(postings.Contains))
        {
            dbContext.BoardCards.Add(BoardCard.ForTrackedJob(userId, id, await NextTopAsync(BoardColumn.Saved),
                BoardCardOrigin.Manual, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AddBoardCardsResponse(applications.Count + postings.Count);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid cardId, CancellationToken cancellationToken)
    {
        var removed = await dbContext.BoardCards
            .Where(c => c.Id == cardId && c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    public async Task<bool> MoveAsync(Guid userId, Guid cardId, MoveBoardCardRequest request, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(userId, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockBoardAsync(userId, cancellationToken);

        var card = await dbContext.BoardCards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId, cancellationToken);
        if (card is null)
        {
            return false;
        }

        if (request.ToStatus is { } toStatus)
        {
            await ChangeStatusThroughBoardAsync(userId, card, toStatus, cancellationToken);
        }

        var column = await ColumnOfAsync(card, cancellationToken);
        await PlaceAsync(userId, card, column, request.AboveCardId, request.BelowCardId, cancellationToken);

        // Touching a card is looking at it: whatever brought it here has been seen.
        card.MarkSeen(_timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task MarkSeenAsync(Guid userId, MarkBoardCardsSeenRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cards = dbContext.BoardCards.Where(c => c.UserId == userId && c.SeenAt == null);
        if (!request.All)
        {
            var ids = request.CardIds ?? [];
            cards = cards.Where(c => ids.Contains(c.Id));
        }

        await cards.ExecuteUpdateAsync(setters => setters.SetProperty(c => c.SeenAt, now), cancellationToken);
    }

    public Task<int> PurgeClosedAsync(CancellationToken cancellationToken)
    {
        var cutoff = ClosedCutoff(_timeProvider.GetUtcNow());
        return dbContext.BoardCards
            .Where(c => c.ClosedAt != null && c.ClosedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// A move that crosses columns is a status change — made through the application service, so
    /// history, reminders and the summary react exactly as they would to the same change anywhere
    /// else (and <see cref="BoardSync"/> lifts the card to the top of its new column). A saved
    /// posting moved into an application column is converted first: that is "I applied".
    /// </summary>
    private async Task ChangeStatusThroughBoardAsync(Guid userId, BoardCard card, ApplicationStatus toStatus,
        CancellationToken cancellationToken)
    {
        if (card.TrackedJobId is { } trackedJobId)
        {
            var created = await trackedJobService.ConvertToApplicationAsync(userId, trackedJobId,
                new ConvertTrackedJobRequest(EmploymentType.FullTime, _timeProvider.GetUtcNow(), Notes: null),
                cancellationToken);
            if (created is null || toStatus == ApplicationStatus.Applied)
            {
                return;
            }

            await applicationService.ChangeStatusAsync(userId, created.Id,
                new ChangeStatusRequest(toStatus, Note: null, ChangedAt: null), cancellationToken);
            return;
        }

        var applicationId = card.ApplicationId!.Value;
        var current = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.UserId == userId)
            .Select(a => (ApplicationStatus?)a.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null || current == toStatus)
        {
            return;
        }

        await applicationService.ChangeStatusAsync(userId, applicationId,
            new ChangeStatusRequest(toStatus, Note: null, ChangedAt: null), cancellationToken);
    }

    private async Task<BoardColumn> ColumnOfAsync(BoardCard card, CancellationToken cancellationToken)
    {
        if (card.ApplicationId is not { } applicationId)
        {
            return BoardColumn.Saved;
        }

        var status = await dbContext.Applications
            .Where(a => a.Id == applicationId)
            .Select(a => a.Status)
            .FirstAsync(cancellationToken);
        return BoardColumns.For(status);
    }

    /// <summary>
    /// Puts the card directly under <paramref name="aboveId"/>, else directly over
    /// <paramref name="belowId"/>, else on top. Only one neighbour is used and the other side is
    /// looked up here: two ids from a client that has scrolled only part of the column can disagree
    /// with the database, one cannot. A neighbour that is not the caller's, or not in this column,
    /// is ignored.
    /// </summary>
    private async Task PlaceAsync(Guid userId, BoardCard card, BoardColumn column, Guid? aboveId, Guid? belowId,
        CancellationToken cancellationToken)
    {
        var others = BoardQueries.CardsIn(dbContext, userId, column).Where(c => c.Id != card.Id);

        var above = aboveId is { } a && a != card.Id
            ? await others.Where(c => c.Id == a).Select(c => new { c.Id, c.Position }).FirstOrDefaultAsync(cancellationToken)
            : null;
        var below = above is null && belowId is { } b && b != card.Id
            ? await others.Where(c => c.Id == b).Select(c => new { c.Id, c.Position }).FirstOrDefaultAsync(cancellationToken)
            : null;

        long? position;
        if (above is not null)
        {
            var next = await others
                .Where(c => c.Position > above.Position || (c.Position == above.Position && c.Id > above.Id))
                .OrderBy(c => c.Position).ThenBy(c => c.Id)
                .Select(c => (long?)c.Position)
                .FirstOrDefaultAsync(cancellationToken);
            position = BoardPositions.Between(above.Position, next);
        }
        else if (below is not null)
        {
            var previous = await others
                .Where(c => c.Position < below.Position || (c.Position == below.Position && c.Id < below.Id))
                .OrderByDescending(c => c.Position).ThenByDescending(c => c.Id)
                .Select(c => (long?)c.Position)
                .FirstOrDefaultAsync(cancellationToken);
            position = BoardPositions.Between(previous, below.Position);
        }
        else
        {
            position = BoardPositions.Top(await others.MinAsync(c => (long?)c.Position, cancellationToken));
        }

        if (position is { } key)
        {
            card.MoveTo(key);
            return;
        }

        // No room left between the two neighbours: renumber the column once, with the card in its
        // new place. Rare (it takes ~20 inserts into the same gap), and the whole column is the
        // caller's own.
        var ordered = await others.OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync(cancellationToken);
        var index = above is not null
            ? ordered.FindIndex(c => c.Id == above.Id) + 1
            : ordered.FindIndex(c => c.Id == below!.Id);
        ordered.Insert(Math.Max(0, index), card);

        var keys = BoardPositions.Renumbered(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].MoveTo(keys[i]);
        }
    }

    private async Task<BoardColumnPage> ReadColumnAsync(Guid userId, BoardColumn column, BoardFilterQuery query,
        BoardCursor? cursor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit ?? 10, 1, 50);
        var rows = column == BoardColumn.Saved
            ? SavedRows(userId, query)
            : ApplicationRows(userId, column, query, now);

        var total = await rows.CountAsync(cancellationToken);

        if (cursor is { } after)
        {
            rows = rows.Where(r => r.Position > after.Position || (r.Position == after.Position && r.CardId > after.CardId));
        }

        var page = await rows
            .OrderBy(r => r.Position).ThenBy(r => r.CardId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var visibleDays = options.Value.ClosedVisibleDays;
        var cards = page.Select(r => new BoardCardResponse(
                r.CardId, r.Kind, r.ItemId, r.JobTitle, r.CompanyId, r.CompanyName, r.Status, r.Source,
                r.LastActivityAt, r.Origin, r.SeenAt == null,
                column == BoardColumn.Closed && r.ClosedAt is { } closedAt ? closedAt.AddDays(visibleDays) : null,
                r.HasCompanyLogo))
            .ToList();

        var nextCursor = hasMore ? new BoardCursor(page[^1].Position, page[^1].CardId).Encode() : null;
        return new BoardColumnPage(column, total, cards, nextCursor);
    }

    private IQueryable<CardRow> ApplicationRows(Guid userId, BoardColumn column, BoardFilterQuery query, DateTimeOffset now)
    {
        var statuses = BoardColumns.StatusesIn(column);
        var terminal = TerminalApplicationStatuses.Values;

        // "Source" on the board is where the posting was, not how the row was recorded: an
        // application saved through the extension is Source.BrowserExtension whatever the site, and
        // the site it came from is on its Job row. Manual entries have no Job and keep their own.
        var rows = from c in dbContext.BoardCards
            join a in dbContext.Applications on c.ApplicationId equals (Guid?)a.Id
            join co in dbContext.Companies on a.CompanyId equals co.Id
            from j in dbContext.Jobs.Where(j => j.Id == a.JobId).DefaultIfEmpty()
            where c.UserId == userId && a.UserId == userId && statuses.Contains(a.Status)
            select new { c, a, CompanyName = co.Name, Source = j != null ? j.Source : a.Source };

        if (column == BoardColumn.Closed)
        {
            var closedCutoff = ClosedCutoff(now);
            rows = rows.Where(x => x.c.ClosedAt == null || x.c.ClosedAt >= closedCutoff);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search.Trim());
            rows = rows.Where(x => EF.Functions.ILike(x.a.JobTitle, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(x.CompanyName, pattern, LikePattern.EscapeCharacter));
        }

        if (query.Sources is { Length: > 0 } sources)
        {
            rows = rows.Where(x => sources.Contains(x.Source));
        }

        if (query.ActiveFrom is { } from)
        {
            rows = rows.Where(x => x.a.UpdatedAt >= from);
        }

        if (query.ActiveTo is { } to)
        {
            rows = rows.Where(x => x.a.UpdatedAt <= to);
        }

        if (query.SilentOnly == true)
        {
            var silentCutoff = now.AddDays(-options.Value.SilentDays);
            rows = rows.Where(x => !terminal.Contains(x.a.Status) && x.a.UpdatedAt <= silentCutoff);
        }

        if (query.WithReminder == true)
        {
            rows = rows.Where(x => dbContext.Reminders.Any(r =>
                r.UserId == userId && r.ApplicationId == x.a.Id && r.DismissedAt == null));
        }

        if (query.WithPromise == true)
        {
            rows = rows.Where(x => x.a.PromisedReplyBy != null && !terminal.Contains(x.a.Status));
        }

        if (query.UnseenOnly == true)
        {
            rows = rows.Where(x => x.c.SeenAt == null);
        }

        return rows.Select(x => new CardRow
        {
            CardId = x.c.Id,
            Position = x.c.Position,
            Kind = BoardCardKind.Application,
            ItemId = x.a.Id,
            JobTitle = x.a.JobTitle,
            CompanyId = x.a.CompanyId,
            CompanyName = x.CompanyName,
            Status = x.a.Status,
            Source = x.Source,
            LastActivityAt = x.a.UpdatedAt,
            Origin = x.c.Origin,
            SeenAt = x.c.SeenAt,
            ClosedAt = x.c.ClosedAt,
            HasCompanyLogo = dbContext.CompanyLogos.Any(l => l.CompanyId == x.a.CompanyId && l.Content != null && !l.Blocked)
        });
    }

    private IQueryable<CardRow> SavedRows(Guid userId, BoardFilterQuery query)
    {
        // A saved posting has no source, no status and nothing to go silent on: any filter about
        // those leaves the column empty rather than pretending the postings match.
        if (query.Sources is { Length: > 0 } || query.SilentOnly == true || query.WithReminder == true || query.WithPromise == true)
        {
            return dbContext.BoardCards.Where(_ => false).Select(c => new CardRow { CardId = c.Id, Position = c.Position });
        }

        var rows = from c in dbContext.BoardCards
            join t in dbContext.TrackedJobs on c.TrackedJobId equals (Guid?)t.Id
            join co in dbContext.Companies on t.CompanyId equals co.Id
            where c.UserId == userId && t.UserId == userId
            select new { c, t, CompanyName = co.Name };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search.Trim());
            rows = rows.Where(x => EF.Functions.ILike(x.t.JobTitle, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(x.CompanyName, pattern, LikePattern.EscapeCharacter));
        }

        if (query.ActiveFrom is { } from)
        {
            rows = rows.Where(x => x.t.UpdatedAt >= from);
        }

        if (query.ActiveTo is { } to)
        {
            rows = rows.Where(x => x.t.UpdatedAt <= to);
        }

        if (query.UnseenOnly == true)
        {
            rows = rows.Where(x => x.c.SeenAt == null);
        }

        return rows.Select(x => new CardRow
        {
            CardId = x.c.Id,
            Position = x.c.Position,
            Kind = BoardCardKind.SavedPosting,
            ItemId = x.t.Id,
            JobTitle = x.t.JobTitle,
            CompanyId = x.t.CompanyId,
            CompanyName = x.CompanyName,
            Status = null,
            Source = null,
            LastActivityAt = x.t.UpdatedAt,
            Origin = x.c.Origin,
            SeenAt = x.c.SeenAt,
            ClosedAt = null,
            HasCompanyLogo = dbContext.CompanyLogos.Any(l => l.CompanyId == x.t.CompanyId && l.Content != null && !l.Blocked)
        });
    }

    /// <summary>
    /// Fills a user's board the first time it is opened: open applications and saved postings
    /// touched within Board:SeedWindowDays, most recent on top. Under the board lock, and marked by
    /// the <see cref="BoardState"/> row in the same transaction, so two tabs opening it at once seed
    /// it once.
    /// </summary>
    private async Task EnsureSeededAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.BoardStates.AnyAsync(s => s.UserId == userId, cancellationToken))
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockBoardAsync(userId, cancellationToken);

        if (await dbContext.BoardStates.AnyAsync(s => s.UserId == userId, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.SeedWindowDays);
        var terminal = TerminalApplicationStatuses.Values;

        var applicationIds = await dbContext.Applications
            .Where(a => a.UserId == userId && !terminal.Contains(a.Status) && a.UpdatedAt >= cutoff)
            .OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        var postingIds = await dbContext.TrackedJobs
            .Where(t => t.UserId == userId && t.UpdatedAt >= cutoff)
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        // One ascending sequence across every column: within any single column the most recent
        // still comes first, which is all a position has to say.
        var applicationKeys = BoardPositions.Renumbered(applicationIds.Count);
        for (var i = 0; i < applicationIds.Count; i++)
        {
            dbContext.BoardCards.Add(BoardCard.ForApplication(userId, applicationIds[i], isClosed: false,
                applicationKeys[i], BoardCardOrigin.Seed, now));
        }

        var postingKeys = BoardPositions.Renumbered(postingIds.Count);
        for (var i = 0; i < postingIds.Count; i++)
        {
            dbContext.BoardCards.Add(BoardCard.ForTrackedJob(userId, postingIds[i], postingKeys[i], BoardCardOrigin.Seed, now));
        }

        dbContext.BoardStates.Add(BoardState.Seeded(userId, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Serialises this user's board writes for the rest of the transaction: seeding, adding and
    /// moving each read a column before writing to it. A key of its own (not the bare user id the
    /// CV lock uses), so a board move never waits on a CV upload. Released by commit or rollback.
    /// </summary>
    private Task LockBoardAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"board:" + userId.ToString()}, 0))", cancellationToken);

    private DateTimeOffset ClosedCutoff(DateTimeOffset now) => now.AddDays(-options.Value.ClosedVisibleDays);

    /// <summary>A flat row both column shapes project to, so paging and cursors are written once.
    /// A class with init setters rather than a positional record: EF has to see through it in the
    /// ORDER BY and WHERE that follow (DECISIONS.md 2026-09-08).</summary>
    private sealed class CardRow
    {
        public Guid CardId { get; init; }
        public long Position { get; init; }
        public BoardCardKind Kind { get; init; }
        public Guid ItemId { get; init; }
        public string JobTitle { get; init; } = string.Empty;
        public Guid CompanyId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public ApplicationStatus? Status { get; init; }
        public Source? Source { get; init; }
        public DateTimeOffset LastActivityAt { get; init; }
        public BoardCardOrigin Origin { get; init; }
        public DateTimeOffset? SeenAt { get; init; }
        public DateTimeOffset? ClosedAt { get; init; }
        public bool HasCompanyLogo { get; init; }
    }
}
