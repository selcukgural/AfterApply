using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// The admin side. Every read starts from <see cref="Visible"/> — published, or the caller's own
/// — so a post another admin has not published yet is "not found" here exactly as it is for a
/// stranger. The caller has already passed <c>IAdminAccessService</c>.
/// </summary>
internal sealed class BlogAdminService(
    AppDbContext dbContext,
    IBlogHtmlSanitizer sanitizer,
    IBlogMediaStorage storage,
    IBlogCacheInvalidator cacheInvalidator,
    IOptions<BlogOptions> options,
    ILogger<BlogAdminService> logger) : IBlogAdminService
{
    public async Task<PagedResult<AdminBlogPostListItemResponse>> ListAsync(Guid adminUserId, AdminBlogListQuery query,
        CancellationToken cancellationToken)
    {
        var posts = Visible(adminUserId);
        if (query.Status is { } status)
        {
            posts = posts.Where(p => p.Status == status);
        }

        if (query.Lang is { } lang)
        {
            posts = posts.Where(p => p.Language == lang);
        }

        var total = await posts.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;

        // Most recently touched first: the table is where an author finds what they were
        // working on, and a publish, an edit and an autosave all count as touching.
        var rows = await posts
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                Post = p,
                AuthorEmail = dbContext.Users.Where(u => u.Id == p.AuthorUserId).Select(u => u.Email).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminBlogPostListItemResponse(
            r.Post.Id, r.Post.Status, r.Post.Language, r.Post.Slug,
            // The draft title is what the author is looking for; it equals the published one
            // until they start editing again.
            r.Post.DraftTitle,
            r.Post.AuthorUserId, r.AuthorEmail, r.Post.AuthorUserId == adminUserId,
            r.Post.UpdatedAt, r.Post.PublishedAt, HasUnpublishedChanges(r.Post))).ToList();

        return new PagedResult<AdminBlogPostListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<AdminBlogPostResponse> CreateAsync(Guid adminUserId, CreateBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var content = Sanitized(request);

        // The validator has already refused a request with nothing written; this repeats it on
        // the sanitized body, which is what would be stored — markup the sanitizer strips is not
        // content either.
        if (!BlogDraftText.HasAny(content.Title, content.Excerpt, content.ContentHtml))
        {
            throw new BlogPostEmptyException();
        }

        var post = BlogPost.Create(adminUserId, request.Language, now);
        dbContext.BlogPosts.Add(post);
        await ApplyDraftAsync(adminUserId, post, request, content, now, cancellationToken);
        await SaveHandlingSlugCollisionAsync(cancellationToken);
        return ToResponse(post, adminUserId, likeCount: 0);
    }

    public async Task<AdminBlogPostResponse?> GetAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        return post is null ? null : ToResponse(post, adminUserId, await LikeCountAsync(postId, cancellationToken));
    }

    public async Task<BlogPostPublicResponse?> PreviewAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // The address the page would get: the slug it has, else the one PublishAsync would
        // allocate from the draft title — computed the same way, so the preview's URL is the
        // real one. Nothing is written here; a title with no slug-able character has no address
        // yet, and publish would refuse it anyway (no title).
        var slug = post.Slug
                   ?? (string.IsNullOrWhiteSpace(post.DraftTitle) ? string.Empty : await AllocateSlugAsync(post.Language, post.DraftTitle, cancellationToken));

        // The same rule as the public query: a twin that is not on the site is not linked.
        var translation = await dbContext.BlogPosts
            .Where(t => t.Id == post.TranslationOfPostId && t.Status == BlogPostStatus.Published)
            .Select(t => new BlogTranslationLink(t.Language, t.Slug!))
            .FirstOrDefaultAsync(cancellationToken);

        // The dates publish would stamp: the first publish date stays, "updated" becomes now.
        return new BlogPostPublicResponse(
            post.Id, slug, post.Language, post.DraftTitle, post.DraftExcerpt, post.DraftContentHtml,
            post.CoverMediaId is { } coverId ? BlogMediaPath.For(coverId) : null,
            post.PublishedAt ?? now, now, await LikeCountAsync(postId, cancellationToken), LikedByMe: null, translation);
    }

    public async Task<BlogDraftSavedResponse?> SaveDraftAsync(Guid adminUserId, Guid postId, SaveBlogDraftRequest request,
        CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // The revision check first, before any field is touched: a stale tab must not be able to
        // change the language or the translation link either.
        if (request.Revision != post.Revision)
        {
            throw new BlogPostRevisionConflictException();
        }

        await SetCoverAsync(post, request.CoverMediaId, now, cancellationToken);
        await ApplyDraftAsync(adminUserId, post, request, Sanitized(request), now, cancellationToken);

        var orphans = await CollectOrphanMediaAsync(post, now, cancellationToken);
        await SaveHandlingSlugCollisionAsync(cancellationToken);

        // The draft slot is invisible to the public; the cover and the translation link are not.
        if (post.IsPublished)
        {
            await cacheInvalidator.InvalidateAsync(cancellationToken);
        }

        await DeleteObjectsAsync(orphans, cancellationToken);

        return new BlogDraftSavedResponse(post.Revision, post.DraftUpdatedAt);
    }

    public async Task<AdminBlogPostResponse?> PublishAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (post.Slug is null)
        {
            // Title → slug, first free one in this language. Two publishes racing on the same
            // title can still pick the same suffix; the unique index catches that and the save
            // below answers "taken" — the next click allocates past it.
            if (string.IsNullOrWhiteSpace(post.DraftTitle))
            {
                throw new BlogPostIncompleteException();
            }

            post.AssignGeneratedSlug(await AllocateSlugAsync(post.Language, post.DraftTitle, cancellationToken), now);
        }
        else if (!post.HasEverBeenPublished && await SlugTakenAsync(post, post.Slug, cancellationToken))
        {
            throw new BlogSlugTakenException();
        }

        post.Publish(now);
        // A publish is when an image dropped from the draft stops being on the site too.
        var orphans = await CollectOrphanMediaAsync(post, now, cancellationToken);
        await SaveHandlingSlugCollisionAsync(cancellationToken);
        await cacheInvalidator.InvalidateAsync(cancellationToken);
        await DeleteObjectsAsync(orphans, cancellationToken);

        return ToResponse(post, adminUserId, await LikeCountAsync(postId, cancellationToken));
    }

    public async Task<AdminBlogPostResponse?> UnpublishAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        post.Unpublish(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cacheInvalidator.InvalidateAsync(cancellationToken);

        return ToResponse(post, adminUserId, await LikeCountAsync(postId, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid adminUserId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await Visible(adminUserId).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post is null)
        {
            return false;
        }

        // Collected before the rows go: the row is the only index into the object.
        var objectNames = await dbContext.BlogMedia
            .Where(m => m.PostId == postId)
            .Select(m => m.ObjectName)
            .ToListAsync(cancellationToken);

        var wasPublished = post.IsPublished;

        // Likes and media rows cascade from the post (their configurations); a post that names
        // this one as its translation is unlinked by the same FK's SET NULL.
        dbContext.BlogPosts.Remove(post);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasPublished)
        {
            await cacheInvalidator.InvalidateAsync(cancellationToken);
        }

        await DeleteObjectsAsync(objectNames, cancellationToken);

        return true;
    }

    /// <summary>
    /// Marks for deletion the post's uploads that nothing points at any more — not the draft,
    /// not the published version, not the cover — and are older than the grace period (see
    /// <see cref="BlogOptions.OrphanMediaGraceMinutes"/> for the race it covers). The rows go
    /// with the caller's SaveChanges; the object names come back so the caller can delete the
    /// bytes after the commit. A cover swap, a re-upload or an image removed from the body
    /// would otherwise leave a reachable object in the bucket until the post itself was deleted.
    /// </summary>
    private async Task<IReadOnlyList<string>> CollectOrphanMediaAsync(BlogPost post, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var referenced = new HashSet<Guid>(BlogMediaPath.ReferencedIn(post.DraftContentHtml));
        referenced.UnionWith(BlogMediaPath.ReferencedIn(post.ContentHtml));
        if (post.CoverMediaId is { } cover)
        {
            referenced.Add(cover);
        }

        var cutoff = now.AddMinutes(-options.Value.OrphanMediaGraceMinutes);
        var orphans = await dbContext.BlogMedia
            .Where(m => m.PostId == post.Id && m.CreatedAt < cutoff && !referenced.Contains(m.Id))
            .ToListAsync(cancellationToken);
        if (orphans.Count == 0)
        {
            return [];
        }

        dbContext.BlogMedia.RemoveRange(orphans);
        return orphans.Select(m => m.ObjectName).ToList();
    }

    /// <summary>After the commit, and best-effort: a failed object delete is a storage leak to
    /// clean up, not a failed request to show the admin. Nothing can read it any more either way.</summary>
    private async Task DeleteObjectsAsync(IReadOnlyList<string> objectNames, CancellationToken cancellationToken)
    {
        foreach (var objectName in objectNames)
        {
            await TryDeleteObjectAsync(objectName, cancellationToken);
        }
    }

    /// <summary>Published, or the caller's own. The one visibility rule, in the query.</summary>
    private IQueryable<BlogPost> Visible(Guid adminUserId) =>
        dbContext.BlogPosts.Where(p => p.Status == BlogPostStatus.Published || p.AuthorUserId == adminUserId);

    /// <summary>Sanitized on the way in, so what is stored is what will be rendered — the public
    /// page never runs a sanitizer of its own.</summary>
    private BlogDraftContent Sanitized(IBlogDraftFields request) => new(
        request.Title.Trim(),
        request.Excerpt?.Trim() ?? string.Empty,
        request.ContentJson,
        sanitizer.Sanitize(request.ContentHtml));

    /// <summary>The form landing on the row — the same steps for the first draft (create) and
    /// every one after (the autosave), so create cannot accept what a save would refuse.</summary>
    private async Task ApplyDraftAsync(Guid adminUserId, BlogPost post, IBlogDraftFields request, BlogDraftContent content,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        post.SetLanguage(request.Language, now);

        // A blank slug means "let publish generate one" before the first publish, and "no
        // change" after it (the editor shows the locked slug and sends it back; an empty field
        // there is not an instruction).
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug.Trim();
        if (slug is not null || !post.HasEverBeenPublished)
        {
            post.SetSlug(slug, now);
        }

        if (slug is not null && await SlugTakenAsync(post, slug, cancellationToken))
        {
            throw new BlogSlugTakenException();
        }

        await LinkTranslationAsync(adminUserId, post, request.TranslationOfPostId, now, cancellationToken);
        post.SaveDraft(content, post.Revision, now);
    }

    private Task<bool> SlugTakenAsync(BlogPost post, string slug, CancellationToken cancellationToken) =>
        dbContext.BlogPosts.AnyAsync(p => p.Id != post.Id && p.Language == post.Language && p.Slug == slug, cancellationToken);

    private async Task<string> AllocateSlugAsync(string language, string title, CancellationToken cancellationToken)
    {
        var baseSlug = BlogSlugGenerator.Generate(title);
        var prefix = baseSlug + "-";

        var taken = await dbContext.BlogPosts
            .Where(p => p.Language == language && p.Slug != null && (p.Slug == baseSlug || p.Slug.StartsWith(prefix)))
            .Select(p => p.Slug!)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = BlogSlugGenerator.WithSuffix(baseSlug, n);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Saves, turning the slug index's 23505 into the domain's "taken". The in-memory
    /// entity is left as it is: the context is scoped to the request and discarded with it.</summary>
    private async Task SaveHandlingSlugCollisionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSlugCollision(exception))
        {
            throw new BlogSlugTakenException();
        }
    }

    private static bool IsSlugCollision(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("Slug", StringComparison.Ordinal) == true;

    /// <summary>Keeps the translation link symmetric: linking A to B links B to A, unlinking A
    /// unlinks whatever pointed back at it. The target must be a different post, in the other
    /// language, that the caller may see.</summary>
    private async Task LinkTranslationAsync(Guid adminUserId, BlogPost post, Guid? targetId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (targetId == post.TranslationOfPostId)
        {
            return;
        }

        if (post.TranslationOfPostId is { } oldTargetId)
        {
            var oldTarget = await dbContext.BlogPosts.FirstOrDefaultAsync(p => p.Id == oldTargetId, cancellationToken);
            if (oldTarget is not null && oldTarget.TranslationOfPostId == post.Id)
            {
                oldTarget.SetTranslationOf(null, now);
            }
        }

        if (targetId is null)
        {
            post.SetTranslationOf(null, now);
            return;
        }

        var target = await Visible(adminUserId).FirstOrDefaultAsync(p => p.Id == targetId, cancellationToken);
        if (target is null || target.Language == post.Language)
        {
            throw new BlogTranslationInvalidException();
        }

        post.SetTranslationOf(target.Id, now);
        target.SetTranslationOf(post.Id, now);
    }

    private async Task SetCoverAsync(BlogPost post, Guid? mediaId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (mediaId == post.CoverMediaId)
        {
            return;
        }

        if (mediaId is { } id && !await dbContext.BlogMedia.AnyAsync(m => m.Id == id && m.PostId == post.Id, cancellationToken))
        {
            throw new BlogCoverInvalidException();
        }

        post.SetCover(mediaId, now);
    }

    private Task<int> LikeCountAsync(Guid postId, CancellationToken cancellationToken) =>
        dbContext.BlogPostLikes.CountAsync(l => l.PostId == postId, cancellationToken);

    private async Task TryDeleteObjectAsync(string objectName, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete stored blog media object {ObjectName}.", objectName);
        }
    }

    /// <summary>A published post whose draft has moved on since the last publish — the table's
    /// "has unpublished changes" hint.</summary>
    private static bool HasUnpublishedChanges(BlogPost post) =>
        post.PublishedUpdatedAt is { } publishedAt && post.DraftUpdatedAt > publishedAt;

    private static AdminBlogPostResponse ToResponse(BlogPost post, Guid adminUserId, int likeCount) => new(
        post.Id, post.Status, post.Language, post.Slug, post.AuthorUserId, post.AuthorUserId == adminUserId,
        post.TranslationOfPostId, post.CoverMediaId,
        post.DraftTitle, post.DraftExcerpt, post.DraftContentJson, post.DraftContentHtml, post.DraftUpdatedAt,
        post.Revision, post.Title, post.PublishedAt, post.PublishedUpdatedAt, HasUnpublishedChanges(post), likeCount, post.CreatedAt);
}
