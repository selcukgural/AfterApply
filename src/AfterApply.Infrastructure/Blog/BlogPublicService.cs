using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// The anonymous side. Every query here starts from <c>Status == Published</c> — the filter is in
/// the query, not in a mapper that could be bypassed — and only the published slot is ever
/// projected. Cached under one tag (<see cref="CacheKeys.Blog"/>) that every write drops.
/// </summary>
internal sealed class BlogPublicService(
    AppDbContext dbContext,
    HybridCache cache,
    IBlogCacheInvalidator cacheInvalidator,
    IOptions<BlogOptions> options,
    ILogger<BlogPublicService> logger) : IBlogPublicService
{
    // The TTL only bounds staleness while Redis is unreachable; every publish, unpublish, edit
    // of a live post and like evicts the tag on every instance.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<PagedResult<BlogPostListItemResponse>> ListAsync(PublicBlogListQuery query, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(CacheKeys.Blog.ListPage(query.Lang, query.Page),
            ct => new ValueTask<PagedResult<BlogPostListItemResponse>>(QueryListAsync(query, ct)),
            CacheOptions, tags: [CacheKeys.Blog.Tag], cancellationToken: cancellationToken);

    public async Task<BlogPostPublicResponse?> GetBySlugAsync(string language, string slug, Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        // The page body is one cache entry for everyone; the one per-reader bit — did *I* like
        // it — is read outside the cache and stamped on afterwards.
        var post = await cache.GetOrCreateAsync(CacheKeys.Blog.Post(language, slug),
            ct => new ValueTask<BlogPostPublicResponse?>(QueryPostAsync(language, slug, ct)),
            CacheOptions, tags: [CacheKeys.Blog.Tag], cancellationToken: cancellationToken);

        if (post is null)
        {
            return null;
        }

        // Every fetch of a published post is a view (2026-09-20): one in-place increment, no
        // record of who, and no cache eviction — the tally is read back fresh here instead of
        // from the cached body, so the page shows the number it just became.
        await dbContext.BlogPosts
            .Where(p => p.Id == post.Id && p.Status == BlogPostStatus.Published)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
        var viewCount = await dbContext.BlogPosts
            .Where(p => p.Id == post.Id)
            .Select(p => p.ViewCount)
            .FirstOrDefaultAsync(cancellationToken);

        var liked = viewerUserId is null
            ? (bool?)null
            : await dbContext.BlogPostLikes.AnyAsync(l => l.PostId == post.Id && l.UserId == viewerUserId, cancellationToken);
        return post with { LikedByMe = liked, ViewCount = viewCount };
    }

    public async Task<IReadOnlyList<BlogSlugResponse>> ListSlugsAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(CacheKeys.Blog.Slugs,
            async ct => (IReadOnlyList<BlogSlugResponse>)await Published()
                .OrderByDescending(p => p.PublishedAt)
                .Select(p => new BlogSlugResponse(p.Language, p.Slug!, p.PublishedAt!.Value, p.PublishedUpdatedAt!.Value,
                    dbContext.BlogPosts
                        .Where(t => t.Id == p.TranslationOfPostId && t.Status == BlogPostStatus.Published)
                        .Select(t => new BlogTranslationLink(t.Language, t.Slug!))
                        .FirstOrDefault()))
                .ToListAsync(ct),
            CacheOptions, tags: [CacheKeys.Blog.Tag], cancellationToken: cancellationToken);

    public async Task<bool> HasPublishedPostsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetOrCreateAsync(CacheKeys.Blog.HasPublished,
                ct => new ValueTask<bool>(Published().AnyAsync(ct)),
                CacheOptions, tags: [CacheKeys.Blog.Tag], cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // /api/config is what the sign-in buttons read; a blog that cannot be counted must
            // not take them down with it. "No blog" is the safe answer.
            logger.LogWarning(exception, "Could not determine whether the blog has published posts.");
            return false;
        }
    }

    public async Task<BlogLikeToggleResponse?> ToggleLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        // An unpublished post is not on any page, so "not found" is what the caller can see.
        if (!await Published().AnyAsync(p => p.Id == postId, cancellationToken))
        {
            return null;
        }

        var existing = await dbContext.BlogPostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId, cancellationToken);
        bool liked;
        if (existing is null)
        {
            dbContext.BlogPostLikes.Add(BlogPostLike.Create(postId, userId, DateTimeOffset.UtcNow));
            liked = true;
        }
        else
        {
            dbContext.BlogPostLikes.Remove(existing);
            liked = false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Double-click: the other request already liked it. Same end state, report it.
            liked = true;
        }

        // The list cards and the page carry the count, so a like is a write to the public blog.
        await cacheInvalidator.InvalidateAsync(cancellationToken);

        var count = await dbContext.BlogPostLikes.CountAsync(l => l.PostId == postId, cancellationToken);
        return new BlogLikeToggleResponse(liked, count);
    }

    public async Task<BlogLikeToggleResponse?> GetLikeStateAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        if (!await Published().AnyAsync(p => p.Id == postId, cancellationToken))
        {
            return null;
        }

        var liked = await dbContext.BlogPostLikes.AnyAsync(l => l.PostId == postId && l.UserId == userId, cancellationToken);
        var count = await dbContext.BlogPostLikes.CountAsync(l => l.PostId == postId, cancellationToken);
        return new BlogLikeToggleResponse(liked, count);
    }

    private IQueryable<BlogPost> Published() =>
        dbContext.BlogPosts.Where(p => p.Status == BlogPostStatus.Published);

    private async Task<PagedResult<BlogPostListItemResponse>> QueryListAsync(PublicBlogListQuery query, CancellationToken cancellationToken)
    {
        var posts = Published().Where(p => p.Language == query.Lang);
        var total = await posts.CountAsync(cancellationToken);
        var pageSize = options.Value.PageSize;

        // The cover URL is composed after materialization: a Guid-to-path concatenation is not
        // something worth asking the database to do.
        var rows = await posts
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Slug, p.Language, p.Title, p.Excerpt, p.CoverMediaId, p.PublishedAt, p.PublishedUpdatedAt,
                LikeCount = dbContext.BlogPostLikes.Count(l => l.PostId == p.Id)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new BlogPostListItemResponse(
            r.Id, r.Slug!, r.Language, r.Title, r.Excerpt, CoverUrl(r.CoverMediaId),
            r.PublishedAt!.Value, r.PublishedUpdatedAt!.Value, r.LikeCount)).ToList();

        return new PagedResult<BlogPostListItemResponse>(items, total, query.Page, pageSize);
    }

    private async Task<BlogPostPublicResponse?> QueryPostAsync(string language, string slug, CancellationToken cancellationToken)
    {
        var row = await Published()
            .Where(p => p.Language == language && p.Slug == slug)
            .Select(p => new
            {
                p.Id, p.Slug, p.Language, p.Title, p.Excerpt, p.ContentHtml, p.CoverMediaId, p.PublishedAt, p.PublishedUpdatedAt,
                p.SeoTitle, p.PrimaryKeyword, p.SecondaryKeywords, p.CoverAlt,
                Cover = dbContext.BlogMedia.Where(m => m.Id == p.CoverMediaId).Select(m => new { m.Width, m.Height }).FirstOrDefault(),
                LikeCount = dbContext.BlogPostLikes.Count(l => l.PostId == p.Id),
                Translation = dbContext.BlogPosts
                    .Where(t => t.Id == p.TranslationOfPostId && t.Status == BlogPostStatus.Published)
                    .Select(t => new BlogTranslationLink(t.Language, t.Slug!))
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new BlogPostPublicResponse(
                row.Id, row.Slug!, row.Language, row.Title, row.Excerpt, row.ContentHtml, CoverUrl(row.CoverMediaId),
                row.PublishedAt!.Value, row.PublishedUpdatedAt!.Value, row.LikeCount, LikedByMe: null, row.Translation,
                ViewCount: 0, row.SeoTitle, row.CoverAlt,
                new BlogSeo(row.SeoTitle, row.PrimaryKeyword, row.SecondaryKeywords, row.CoverAlt).AllKeywords,
                row.Cover?.Width, row.Cover?.Height);
    }

    private static string? CoverUrl(Guid? coverMediaId) =>
        coverMediaId is { } id ? BlogMediaPath.For(id) : null;
}
