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
    BlogTranslationLink? Translation,
    int ViewCount = 0,
    string? SeoTitle = null,
    string? CoverAlt = null,
    IReadOnlyList<string>? Keywords = null);

/// <summary>
/// What the model proposed for the draft (DECISIONS.md 2026-09-21). Every field may be null —
/// the editor shows what came and offers "apply" per field; nothing is written to the post
/// here. <paramref name="Slug"/> only comes for a post that has never been published (the
/// address is locked after). <paramref name="IntentNote"/> is one sentence of advice about the
/// search intent, shown, never stored.
/// </summary>
public sealed record BlogSeoSuggestionResponse(
    string? SeoTitle,
    string? MetaDescription,
    string? PrimaryKeyword,
    IReadOnlyList<string> SecondaryKeywords,
    string? CoverAlt,
    string? Slug,
    string? IntentNote);

/// <summary>The draft's SEO fields, as the editor holds them (DECISIONS.md 2026-09-21).</summary>
public sealed record BlogSeoResponse(
    string? SeoTitle,
    string? PrimaryKeyword,
    IReadOnlyList<string> SecondaryKeywords,
    string? CoverAlt);

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
    bool HasUnpublishedChanges,
    int LikeCount,
    int ViewCount,
    Guid? TranslationOfPostId);

/// <summary>
/// One row of the admin table (2026-09-20): a post and its translation side by side, either
/// side null when there is no such post — or when there is one the caller may not see (another
/// admin's draft), which the visible side's <see cref="AdminBlogPostListItemResponse.TranslationOfPostId"/>
/// still points at. <paramref name="UpdatedAt"/> is the later of the two, the row's sort key.
/// </summary>
public sealed record AdminBlogPostGroupResponse(
    AdminBlogPostListItemResponse? Tr,
    AdminBlogPostListItemResponse? En,
    DateTimeOffset UpdatedAt);

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
    DateTimeOffset CreatedAt,
    int ViewCount = 0,
    BlogSeoResponse? DraftSeo = null);

public sealed record BlogDraftSavedResponse(int Revision, DateTimeOffset DraftUpdatedAt);

/// <summary><paramref name="Url"/> is the relative path the editor inserts as the image's
/// <c>src</c> — see <see cref="BlogMediaPath"/> for why it is relative.</summary>
public sealed record BlogMediaResponse(Guid Id, string Url, string ContentType, long ByteSize, int? Width, int? Height);

/// <summary>An opened image: the bytes, their type, and whether they may be cached publicly
/// (the post is published) or must stay private to the author's browser.</summary>
public sealed record BlogMediaContent(Stream Content, string ContentType, bool IsPublic);
