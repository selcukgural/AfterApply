using AfterApply.Application.Blog.Contracts;

namespace AfterApply.Application.Blog;

/// <summary>What the model gets: the draft's own words, nothing about who wrote it or who reads
/// the site. <paramref name="BodyText"/> is the body with its markup removed, already cut to the
/// provider's input cap by the caller or the provider — either is fine, the provider cuts again.</summary>
public sealed record BlogSeoSuggestionRequest(
    string Language,
    string Title,
    string Excerpt,
    string BodyText,
    /// <summary>Null when the post has never been published: the model may then propose one.</summary>
    string? LockedSlug,
    bool HasCover);

/// <summary>
/// One call to the model for a draft's SEO fields (DECISIONS.md 2026-09-21). The provider owns
/// the prompt, the schema and the parsing; the service owns visibility, what text goes in and the
/// caps on what comes out. Faked in the integration tests, so an admin's click never reaches
/// Vertex from a test.
/// </summary>
public interface IBlogSeoSuggestionProvider
{
    Task<BlogSeoSuggestionResponse> SuggestAsync(BlogSeoSuggestionRequest request, CancellationToken cancellationToken);
}
