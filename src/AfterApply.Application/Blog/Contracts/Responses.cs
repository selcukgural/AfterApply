using AfterApply.Domain.Blog;

namespace AfterApply.Application.Blog.Contracts;

/// <summary>Where the same post lives in the other language, when the author linked one.</summary>
public sealed record BlogTranslationLink(string Language, string Slug);

/// <summary>A card on the public list. The published slot only; no author.</summary>
public sealed record BlogPostListItemResponse(
    Guid Id,
    string Slug,
    string Language,
    string Title,
    string Excerpt,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    int LikeCount);

/// <summary>The public page. <paramref name="ContentHtml"/> is the sanitized published slot.
/// <paramref name="LikedByMe"/> is null for an anonymous reader — the route never asks for a
/// token, it only uses one that happens to be there.</summary>
public sealed record BlogPostPublicResponse(
    Guid Id,
    string Slug,
    string Language,
    string Title,
    string Excerpt,
    string ContentHtml,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    int LikeCount,
    bool? LikedByMe,
    BlogTranslationLink? Translation);

/// <summary>One sitemap entry per published post.</summary>
public sealed record BlogSlugResponse(
    string Language,
    string Slug,
    DateTimeOffset PublishedAt,
    DateTimeOffset UpdatedAt,
    BlogTranslationLink? Translation);

public sealed record BlogLikeToggleResponse(bool Liked, int LikeCount);

/// <summary>A row of the admin table. Drafts appear here only for their author (the service
/// filters), so <paramref name="AuthorEmail"/> on a draft is always the caller's own.</summary>
public sealed record AdminBlogPostListItemResponse(
    Guid Id,
    BlogPostStatus Status,
    string Language,
    string? Slug,
    string Title,
    Guid? AuthorUserId,
    string? AuthorEmail,
    bool IsMine,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    bool HasUnpublishedChanges);

/// <summary>Everything the editor needs to open a post: the draft slot to edit, the published
/// dates to show, the revision to send back with the next save.</summary>
public sealed record AdminBlogPostResponse(
    Guid Id,
    BlogPostStatus Status,
    string Language,
    string? Slug,
    Guid? AuthorUserId,
    bool IsMine,
    Guid? TranslationOfPostId,
    Guid? CoverMediaId,
    string DraftTitle,
    string DraftExcerpt,
    string DraftContentJson,
    string DraftContentHtml,
    DateTimeOffset DraftUpdatedAt,
    int Revision,
    string PublishedTitle,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? PublishedUpdatedAt,
    bool HasUnpublishedChanges,
    int LikeCount,
    DateTimeOffset CreatedAt);

public sealed record BlogDraftSavedResponse(int Revision, DateTimeOffset DraftUpdatedAt);

/// <summary><paramref name="Url"/> is the relative path the editor inserts as the image's
/// <c>src</c> — see <see cref="BlogMediaPath"/> for why it is relative.</summary>
public sealed record BlogMediaResponse(Guid Id, string Url, string ContentType, long ByteSize, int? Width, int? Height);

/// <summary>An opened image: the bytes, their type, and whether they may be cached publicly
/// (the post is published) or must stay private to the author's browser.</summary>
public sealed record BlogMediaContent(Stream Content, string ContentType, bool IsPublic);
