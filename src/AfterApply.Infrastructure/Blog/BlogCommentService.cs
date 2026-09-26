using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// Reader comments (DECISIONS.md 2026-09-20). Every read that reaches a page starts from an
/// approved comment on a published post; the reader's own pending comments are the one thing
/// added on top, for the reader alone. No cache: the lists are one indexed query each and a
/// comment must show the moment it is approved.
/// </summary>
internal sealed class BlogCommentService(
    AppDbContext dbContext,
    IOptions<BlogOptions> options,
    ContributionNotificationWriter notifications) : IBlogCommentService
{
    // ---- the reader's side ---------------------------------------------------------------------

    public async Task<BlogCommentListResponse?> ListAsync(Guid postId, Guid? viewerUserId, PublicBlogCommentListQuery query,
        CancellationToken cancellationToken)
    {
        if (!await IsPublishedAsync(postId, cancellationToken))
        {
            return null;
        }

        // Approved for everyone, plus the viewer's own pending ones: the author sees their comment
        // where it will land, with its "waiting" badge, and nobody else sees it at all.
        var visible = dbContext.BlogComments.Where(c => c.PostId == postId
            && (c.Status == BlogCommentStatus.Approved || (viewerUserId != null && c.UserId == viewerUserId && c.Status == BlogCommentStatus.Pending)));

        var roots = visible.Where(c => c.ParentCommentId == null);
        var totalRoots = await roots.CountAsync(cancellationToken);
        var totalApproved = await dbContext.BlogComments.CountAsync(c => c.PostId == postId && c.Status == BlogCommentStatus.Approved, cancellationToken);

        var pageSize = options.Value.CommentPageSize;
        var pageRoots = await roots
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var rootIds = pageRoots.Select(c => c.Id).ToList();
        var replies = await visible
            .Where(c => c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId.Value))
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var all = pageRoots.Concat(replies).ToList();
        var allIds = all.Select(c => c.Id).ToList();
        var authors = await AuthorNamesAsync(all.Select(c => c.UserId), cancellationToken);
        var helpful = await HelpfulCountsAsync(allIds, cancellationToken);
        var mine = viewerUserId is { } viewer
            ? (await dbContext.BlogCommentHelpfulVotes
                .Where(v => v.UserId == viewer && allIds.Contains(v.CommentId))
                .Select(v => v.CommentId)
                .ToListAsync(cancellationToken)).ToHashSet()
            : null;

        BlogCommentResponse Map(BlogComment c, IReadOnlyList<BlogCommentResponse> children) => new(
            c.Id, c.PostId, c.ParentCommentId, c.Content, c.Status,
            authors.GetValueOrDefault(c.UserId), viewerUserId == c.UserId, c.CreatedAt, c.EditedAt,
            helpful.GetValueOrDefault(c.Id), mine?.Contains(c.Id), children);

        var items = pageRoots
            .Select(root => Map(root, replies.Where(r => r.ParentCommentId == root.Id).Select(r => Map(r, [])).ToList()))
            .ToList();

        return new BlogCommentListResponse(items, totalApproved, totalRoots, query.Page, pageSize);
    }

    public async Task<BlogCommentResponse?> CreateAsync(Guid userId, Guid postId, CreateBlogCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsPublishedAsync(postId, cancellationToken))
        {
            return null;
        }

        return await AddAsync(userId, postId, parentCommentId: null, request.Content, cancellationToken);
    }

    public async Task<BlogCommentResponse?> ReplyAsync(Guid userId, Guid parentCommentId, CreateBlogCommentRequest request,
        CancellationToken cancellationToken)
    {
        var parent = await dbContext.BlogComments
            .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.Status == BlogCommentStatus.Approved, cancellationToken);
        if (parent is null || !await IsPublishedAsync(parent.PostId, cancellationToken))
        {
            return null;
        }

        // One level only: answering a reply answers its root, so the thread never nests. The root
        // must itself still be on the page.
        var rootId = parent.ParentCommentId ?? parent.Id;
        if (rootId != parent.Id
            && !await dbContext.BlogComments.AnyAsync(c => c.Id == rootId && c.Status == BlogCommentStatus.Approved, cancellationToken))
        {
            return null;
        }

        return await AddAsync(userId, parent.PostId, rootId, request.Content, cancellationToken);
    }

    private async Task<BlogCommentResponse> AddAsync(Guid userId, Guid postId, Guid? parentCommentId, string content,
        CancellationToken cancellationToken)
    {
        var normalized = BlogComment.Normalize(content);
        // A double click, a "did it go through?" resend: the same reader, the same post, the same
        // text is never a second comment — whatever the first one's status.
        if (await dbContext.BlogComments.AnyAsync(c => c.PostId == postId && c.UserId == userId && c.Content == normalized, cancellationToken))
        {
            throw new BlogCommentDuplicateException();
        }

        // An admin's own comment skips the queue they would be approving it from.
        var author = await dbContext.Users.Where(u => u.Id == userId)
            .Select(u => new { u.IsAdmin, u.FirstName, u.LastName })
            .FirstAsync(cancellationToken);
        var comment = BlogComment.Create(postId, userId, parentCommentId, normalized, approved: author.IsAdmin, DateTimeOffset.UtcNow);
        dbContext.BlogComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(comment, BlogCommentAuthorName.Format(author.FirstName, author.LastName), isMine: true, helpfulCount: 0, helpfulByMe: false);
    }

    public async Task<BlogCommentResponse?> EditAsync(Guid userId, Guid commentId, EditBlogCommentRequest request,
        CancellationToken cancellationToken)
    {
        // Someone else's comment is "not found", not "forbidden": its existence is not the caller's to learn.
        var comment = await dbContext.BlogComments.FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId, cancellationToken);
        if (comment is null)
        {
            return null;
        }

        comment.Edit(request.Content, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var name = await AuthorNamesAsync([userId], cancellationToken);
        return ToResponse(comment, name.GetValueOrDefault(userId), isMine: true,
            await HelpfulCountAsync(commentId, cancellationToken), helpfulByMe: false);
    }

    public async Task<BlogCommentHelpfulResponse?> ToggleHelpfulAsync(Guid userId, Guid commentId, CancellationToken cancellationToken)
    {
        var authorUserId = await dbContext.BlogComments
            .Where(c => c.Id == commentId && c.Status == BlogCommentStatus.Approved)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (authorUserId is null)
        {
            return null;
        }

        var existing = await dbContext.BlogCommentHelpfulVotes
            .FirstOrDefaultAsync(v => v.CommentId == commentId && v.UserId == userId, cancellationToken);
        bool helpful;
        var raced = false;
        if (existing is null)
        {
            dbContext.BlogCommentHelpfulVotes.Add(BlogCommentHelpfulVote.Create(commentId, userId, DateTimeOffset.UtcNow));
            helpful = true;
        }
        else
        {
            dbContext.BlogCommentHelpfulVotes.Remove(existing);
            helpful = false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Double-click: the other request already voted — and told the author. Same end state.
            helpful = true;
            raced = true;
        }

        // Voting on one's own comment is allowed here; the writer is what skips telling oneself.
        if (helpful && !raced)
        {
            await notifications.RecordHelpfulAsync(ContributionNotificationType.BlogCommentHelpful, commentId, authorUserId.Value, userId,
                cancellationToken);
        }

        return new BlogCommentHelpfulResponse(helpful, await HelpfulCountAsync(commentId, cancellationToken));
    }

    public async Task<BlogCommentReportResponse?> ReportAsync(Guid userId, Guid commentId, ReportBlogCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.BlogComments.AnyAsync(c => c.Id == commentId && c.Status == BlogCommentStatus.Approved, cancellationToken))
        {
            return null;
        }

        var existing = await dbContext.BlogCommentReports
            .FirstOrDefaultAsync(r => r.CommentId == commentId && r.ReporterUserId == userId, cancellationToken);
        if (existing is not null)
        {
            return new BlogCommentReportResponse(existing.Id, AlreadyReported: true);
        }

        var report = BlogCommentReport.Create(commentId, userId, request.Reason, request.Note, DateTimeOffset.UtcNow);
        dbContext.BlogCommentReports.Add(report);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            var raced = await dbContext.BlogCommentReports
                .AsNoTracking()
                .FirstAsync(r => r.CommentId == commentId && r.ReporterUserId == userId, cancellationToken);
            return new BlogCommentReportResponse(raced.Id, AlreadyReported: true);
        }

        return new BlogCommentReportResponse(report.Id, AlreadyReported: false);
    }

    public async Task<PagedResult<MyBlogCommentResponse>> ListMineAsync(Guid userId, MyBlogCommentListQuery query,
        CancellationToken cancellationToken)
    {
        var mine = dbContext.BlogComments.Where(c => c.UserId == userId);
        var total = await mine.CountAsync(cancellationToken);
        var pageSize = options.Value.PageSize;
        var items = await MineAsync(mine
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize), cancellationToken);

        return new PagedResult<MyBlogCommentResponse>(items, total, query.Page, pageSize);
    }

    public async Task<IReadOnlyList<MyBlogCommentResponse>> ListMineByIdsAsync(Guid userId, IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken)
    {
        if (commentIds.Count == 0)
        {
            return [];
        }

        // The caller's id is in the filter: an id that is someone else's comes back as nothing.
        return await MineAsync(dbContext.BlogComments.Where(c => c.UserId == userId && commentIds.Contains(c.Id)), cancellationToken);
    }

    private async Task<List<MyBlogCommentResponse>> MineAsync(IQueryable<BlogComment> comments, CancellationToken cancellationToken)
    {
        var rows = await comments
            .Select(c => new
            {
                Comment = c,
                Post = dbContext.BlogPosts.Where(p => p.Id == c.PostId).Select(p => new { p.Title, p.Slug, p.Language }).First(),
                ParentUserId = dbContext.BlogComments.Where(p => p.Id == c.ParentCommentId).Select(p => (Guid?)p.UserId).FirstOrDefault(),
                HelpfulCount = dbContext.BlogCommentHelpfulVotes.Count(v => v.CommentId == c.Id),
                ReplyCount = dbContext.BlogComments.Count(r => r.ParentCommentId == c.Id && r.Status == BlogCommentStatus.Approved)
            })
            .ToListAsync(cancellationToken);

        var authors = await AuthorNamesAsync(rows.Where(r => r.ParentUserId != null).Select(r => r.ParentUserId!.Value), cancellationToken);
        return rows.Select(r => new MyBlogCommentResponse(
            r.Comment.Id, r.Comment.PostId, r.Post.Title, r.Post.Slug ?? string.Empty, r.Post.Language,
            r.Comment.ParentCommentId, r.ParentUserId is { } parentUser ? authors.GetValueOrDefault(parentUser) : null,
            r.Comment.Content, r.Comment.Status, r.Comment.CreatedAt, r.Comment.EditedAt, r.HelpfulCount, r.ReplyCount)).ToList();
    }

    // ---- the admin's side ----------------------------------------------------------------------

    public async Task<PagedResult<AdminBlogCommentListItemResponse>> ListForAdminAsync(AdminBlogCommentListQuery query,
        CancellationToken cancellationToken)
    {
        var comments = dbContext.BlogComments.AsQueryable();
        if (query.Status is { } status)
        {
            comments = comments.Where(c => c.Status == status);
        }

        if (query.Reported)
        {
            comments = comments.Where(c => dbContext.BlogCommentReports.Any(r => r.CommentId == c.Id && r.Status == BlogCommentReportStatus.Open));
        }

        var total = await comments.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;
        var rows = await comments
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(AdminRow)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToAdminItem).ToList();
        return new PagedResult<AdminBlogCommentListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<AdminBlogCommentResponse?> GetForAdminAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var row = await dbContext.BlogComments.Where(c => c.Id == commentId).Select(AdminRow).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var parentContent = row.Comment.ParentCommentId is { } parentId
            ? await dbContext.BlogComments.Where(c => c.Id == parentId).Select(c => c.Content).FirstOrDefaultAsync(cancellationToken)
            : null;
        var reports = await dbContext.BlogCommentReports
            .Where(r => r.CommentId == commentId)
            .OrderByDescending(r => r.ReportedAt)
            .Select(r => new AdminBlogCommentReportResponse(r.Id, r.Reason, r.Note, r.Status, r.ReportedAt, r.ResolvedAt))
            .ToListAsync(cancellationToken);

        return new AdminBlogCommentResponse(ToAdminItem(row), parentContent, reports);
    }

    public Task<AdminBlogCommentResponse?> ApproveAsync(Guid adminUserId, Guid commentId, CancellationToken cancellationToken) =>
        ModerateAsync(adminUserId, commentId, approve: true, cancellationToken);

    public Task<AdminBlogCommentResponse?> RejectAsync(Guid adminUserId, Guid commentId, CancellationToken cancellationToken) =>
        ModerateAsync(adminUserId, commentId, approve: false, cancellationToken);

    private async Task<AdminBlogCommentResponse?> ModerateAsync(Guid adminUserId, Guid commentId, bool approve,
        CancellationToken cancellationToken)
    {
        var comment = await dbContext.BlogComments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (approve)
        {
            comment.Approve(adminUserId, now);
        }
        else
        {
            comment.Reject(adminUserId, now);
            // Rejecting is the action every open report asked for. The replies are left as they
            // are: the page shows a reply only under an approved root, so they go dark with it.
            foreach (var report in await OpenReportsAsync(commentId, cancellationToken))
            {
                report.Resolve(BlogCommentReportStatus.ActionTaken, adminUserId, now);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetForAdminAsync(commentId, cancellationToken);
    }

    public async Task<AdminBlogCommentResponse?> DismissReportsAsync(Guid adminUserId, Guid commentId, CancellationToken cancellationToken)
    {
        if (!await dbContext.BlogComments.AnyAsync(c => c.Id == commentId, cancellationToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var report in await OpenReportsAsync(commentId, cancellationToken))
        {
            report.Resolve(BlogCommentReportStatus.Dismissed, adminUserId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetForAdminAsync(commentId, cancellationToken);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken) =>
        dbContext.BlogComments.CountAsync(c => c.Status == BlogCommentStatus.Pending, cancellationToken);

    // ---- helpers -------------------------------------------------------------------------------

    private Task<bool> IsPublishedAsync(Guid postId, CancellationToken cancellationToken) =>
        // Guides take no comments (DECISIONS.md 2026-09-26): to this service a guide is a post
        // that is not there, so every comment route answers it with 404.
        dbContext.BlogPosts.AnyAsync(p => p.Id == postId && p.Status == BlogPostStatus.Published && p.Kind == BlogPostKind.Blog,
            cancellationToken);

    private Task<List<BlogCommentReport>> OpenReportsAsync(Guid commentId, CancellationToken cancellationToken) =>
        dbContext.BlogCommentReports
            .Where(r => r.CommentId == commentId && r.Status == BlogCommentReportStatus.Open)
            .ToListAsync(cancellationToken);

    private Task<int> HelpfulCountAsync(Guid commentId, CancellationToken cancellationToken) =>
        dbContext.BlogCommentHelpfulVotes.CountAsync(v => v.CommentId == commentId, cancellationToken);

    private async Task<Dictionary<Guid, int>> HelpfulCountsAsync(IEnumerable<Guid> commentIds, CancellationToken cancellationToken)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.BlogCommentHelpfulVotes
            .Where(v => ids.Contains(v.CommentId))
            .GroupBy(v => v.CommentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
    }

    /// <summary>The page name of each author — first name and last initial, or null. Only the
    /// name columns are read; the address never leaves the query.</summary>
    private async Task<Dictionary<Guid, string?>> AuthorNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(u => u.Id, u => BlogCommentAuthorName.Format(u.FirstName, u.LastName));
    }

    private static BlogCommentResponse ToResponse(BlogComment c, string? authorName, bool isMine, int helpfulCount, bool? helpfulByMe) =>
        new(c.Id, c.PostId, c.ParentCommentId, c.Content, c.Status, authorName, isMine, c.CreatedAt, c.EditedAt,
            helpfulCount, helpfulByMe, []);

    private sealed record AdminRowData(
        BlogComment Comment,
        string PostTitle,
        string? PostSlug,
        string PostLanguage,
        string? AuthorFirstName,
        string? AuthorLastName,
        string? AuthorEmail,
        string? ParentFirstName,
        string? ParentLastName,
        int OpenReportCount,
        int HelpfulCount,
        int ReplyCount);

    private System.Linq.Expressions.Expression<Func<BlogComment, AdminRowData>> AdminRow => c => new AdminRowData(
        c,
        dbContext.BlogPosts.Where(p => p.Id == c.PostId).Select(p => p.Title).First(),
        dbContext.BlogPosts.Where(p => p.Id == c.PostId).Select(p => p.Slug).First(),
        dbContext.BlogPosts.Where(p => p.Id == c.PostId).Select(p => p.Language).First(),
        dbContext.Users.Where(u => u.Id == c.UserId).Select(u => u.FirstName).FirstOrDefault(),
        dbContext.Users.Where(u => u.Id == c.UserId).Select(u => u.LastName).FirstOrDefault(),
        dbContext.Users.Where(u => u.Id == c.UserId).Select(u => u.Email).FirstOrDefault(),
        dbContext.BlogComments.Where(p => p.Id == c.ParentCommentId).Join(dbContext.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName).FirstOrDefault(),
        dbContext.BlogComments.Where(p => p.Id == c.ParentCommentId).Join(dbContext.Users, p => p.UserId, u => u.Id, (p, u) => u.LastName).FirstOrDefault(),
        dbContext.BlogCommentReports.Count(r => r.CommentId == c.Id && r.Status == BlogCommentReportStatus.Open),
        dbContext.BlogCommentHelpfulVotes.Count(v => v.CommentId == c.Id),
        dbContext.BlogComments.Count(r => r.ParentCommentId == c.Id));

    private static AdminBlogCommentListItemResponse ToAdminItem(AdminRowData r) => new(
        r.Comment.Id, r.Comment.PostId, r.PostTitle, r.PostSlug, r.PostLanguage, r.Comment.ParentCommentId,
        r.Comment.ParentCommentId is null ? null : BlogCommentAuthorName.Format(r.ParentFirstName, r.ParentLastName),
        BlogCommentAuthorName.Format(r.AuthorFirstName, r.AuthorLastName), r.AuthorEmail,
        r.Comment.Content, r.Comment.Status, r.OpenReportCount, r.HelpfulCount, r.ReplyCount,
        r.Comment.CreatedAt, r.Comment.EditedAt);
}
