using AfterApply.Domain.Blog;

namespace AfterApply.Application.Blog.Contracts;

/// <summary>Creates an empty draft in one language; everything else arrives by autosave.</summary>
public sealed record CreateBlogPostRequest(string Language);

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
    int Revision);

/// <summary>The public list of one language, newest first.</summary>
public sealed record PublicBlogListQuery(string Lang, int Page = 1);

/// <summary>The admin table: optionally one status or one language, newest first.</summary>
public sealed record AdminBlogListQuery(BlogPostStatus? Status = null, string? Lang = null, int Page = 1);
