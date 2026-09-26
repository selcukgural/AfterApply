using AfterApply.Application.Blog.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Blog;

/// <summary>
/// A guide's "related" ids turned into links (2026-09-26). The ids are stored as the author picked
/// them; what the page shows is decided at read time — only guides that are published, in the
/// same language, in the author's order. One that was unpublished or deleted since simply drops
/// out, so the stored list never has to be cleaned up after another post changes.
/// </summary>
internal static class BlogRelatedLinks
{
    public static async Task<IReadOnlyList<BlogRelatedLink>> ResolveAsync(AppDbContext dbContext, IReadOnlyList<Guid> ids,
        string language, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.BlogPosts
            .Where(p => ids.Contains(p.Id)
                        && p.Kind == BlogPostKind.Guide
                        && p.Status == BlogPostStatus.Published
                        && p.Language == language)
            .Select(p => new { p.Id, p.Slug, p.Title, p.Excerpt })
            .ToListAsync(cancellationToken);

        return
        [
            .. ids.Select(id => rows.FirstOrDefault(r => r.Id == id)).Where(r => r is not null)
                  .Select(r => new BlogRelatedLink(r!.Slug!, r.Title, r.Excerpt))
        ];
    }
}
