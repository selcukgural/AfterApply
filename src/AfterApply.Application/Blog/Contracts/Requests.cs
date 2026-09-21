using AfterApply.Domain.Blog;

namespace AfterApply.Application.Blog.Contracts;

/// <summary>What create and the autosave have in common: the whole form. One validator covers
/// both (<c>BlogDraftFieldsValidator</c>), so a rule cannot hold for one and not the other.</summary>
public interface IBlogDraftFields
{
    string Title { get; }
    string? Excerpt { get; }
    string ContentJson { get; }
    string ContentHtml { get; }
    string Language { get; }
    string? Slug { get; }
    Guid? TranslationOfPostId { get; }
    /// <summary>Null from an editor build that predates the SEO fields (2026-09-21): stored as
    /// "none of them", never as an error.</summary>
    BlogSeoRequest? Seo { get; }
}

/// <summary>The SEO fields of the form (DECISIONS.md 2026-09-21). Every one optional.</summary>
public sealed record BlogSeoRequest(
    string? SeoTitle,
    string? PrimaryKeyword,
    IReadOnlyList<string>? SecondaryKeywords,
    string? CoverAlt);

/// <summary>
/// Creates a post from its first draft. There is no "empty post" (2026-09-19): the editor opens
/// on nothing and sends this once the author has typed at least one character into the title,
/// the summary or the body — a request with none of the three is refused
/// (<c>BLOG_POST_EMPTY</c>), so a "new post" that was opened and abandoned leaves no row. No
/// cover: an image belongs to a post, so there is none to attach before the post exists.
/// </summary>
public sealed record CreateBlogPostRequest(
    string Title,
    string? Excerpt,
    string ContentJson,
    string ContentHtml,
    string Language,
    string? Slug,
    Guid? TranslationOfPostId,
    BlogSeoRequest? Seo = null) : IBlogDraftFields;

/// <summary>
/// The autosave. Everything editable on a post travels together: the editor does not know which
/// field changed, and one shape for the whole form is what lets it save on a timer. Language and
/// slug are refused once the post has been published (<c>BLOG_POST_FIELD_LOCKED</c>);
/// <paramref name="Revision"/> is the one the editor last saw, and a mismatch is a 409.
/// </summary>
public sealed record SaveBlogDraftRequest(
    string Title,
    string? Excerpt,
    string ContentJson,
    string ContentHtml,
    string Language,
    string? Slug,
    Guid? CoverMediaId,
    Guid? TranslationOfPostId,
    int Revision,
    BlogSeoRequest? Seo = null) : IBlogDraftFields;

/// <summary>The public list of one language, newest first.</summary>
public sealed record PublicBlogListQuery(string Lang, int Page = 1);

/// <summary>The admin table: optionally one status or one language, newest first.</summary>
public sealed record AdminBlogListQuery(BlogPostStatus? Status = null, string? Lang = null, int Page = 1);

/// <summary>The admin table's query: one row per post-and-translation pair. No language — a row
/// shows both. A status filter keeps a row when either side matches, and the row still shows both.</summary>
public sealed record AdminBlogGroupedListQuery(BlogPostStatus? Status = null, int Page = 1);
