using AfterApply.Domain.Blog;

namespace AfterApply.Application.Blog.Contracts;

// ---- requests ----------------------------------------------------------------------------------

/// <summary>A new root comment on a post, or a reply (the route names the parent). Plain text:
/// what arrives is stored and rendered as text, never as markup.</summary>
public sealed record CreateBlogCommentRequest(string Content);

/// <summary>The author's edit while the comment is pending.</summary>
public sealed record EditBlogCommentRequest(string Content);

public sealed record ReportBlogCommentRequest(BlogCommentReportReason Reason, string? Note);

/// <summary>The page's list: root comments newest first, 10 a page, each with all its replies.</summary>
public sealed record PublicBlogCommentListQuery(int Page = 1);

public sealed record MyBlogCommentListQuery(int Page = 1);

/// <summary>The admin table. <paramref name="Reported"/> narrows to comments with an open report.</summary>
public sealed record AdminBlogCommentListQuery(BlogCommentStatus? Status = null, bool Reported = false, int Page = 1);

// ---- the reader's side -------------------------------------------------------------------------

/// <summary>
/// One comment as the page shows it. <paramref name="AuthorName"/> is the first name plus the
/// last initial ("Selin Y."), or null when the account has no name — the page then says "a
/// reader"; the address, the id and anything else about the account never travel.
/// <paramref name="Status"/> is Approved for everyone else's comment and whatever it is for the
/// viewer's own (a pending one is on the list for its author alone). <paramref name="HelpfulByMe"/>
/// is null for an anonymous reader.
/// </summary>
public sealed record BlogCommentResponse(
    Guid Id,
    Guid PostId,
    Guid? ParentCommentId,
    string Content,
    BlogCommentStatus Status,
    string? AuthorName,
    bool IsMine,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    int HelpfulCount,
    bool? HelpfulByMe,
    IReadOnlyList<BlogCommentResponse> Replies);

/// <summary><paramref name="TotalCount"/> counts every approved comment, replies included — the
/// "Yorumlar (12)" number; <paramref name="TotalRootCount"/> is what the pages are cut from.</summary>
public sealed record BlogCommentListResponse(
    IReadOnlyList<BlogCommentResponse> Items,
    int TotalCount,
    int TotalRootCount,
    int Page,
    int PageSize);

public sealed record BlogCommentHelpfulResponse(bool Helpful, int HelpfulCount);

/// <summary><paramref name="AlreadyReported"/>: the reader had reported this comment before, and
/// that report is the one answered — no second row.</summary>
public sealed record BlogCommentReportResponse(Guid Id, bool AlreadyReported);

/// <summary>A reader's own comment on their contributions list (Katkılarım), whatever its status.</summary>
public sealed record MyBlogCommentResponse(
    Guid Id,
    Guid PostId,
    string PostTitle,
    string PostSlug,
    string PostLanguage,
    Guid? ParentCommentId,
    string? ParentAuthorName,
    string Content,
    BlogCommentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    int HelpfulCount,
    int ReplyCount);

// ---- the admin's side --------------------------------------------------------------------------

public sealed record AdminBlogCommentListItemResponse(
    Guid Id,
    Guid PostId,
    string PostTitle,
    string? PostSlug,
    string PostLanguage,
    Guid? ParentCommentId,
    string? ParentAuthorName,
    string? AuthorName,
    string? AuthorEmail,
    string Content,
    BlogCommentStatus Status,
    int OpenReportCount,
    int HelpfulCount,
    int ReplyCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);

public sealed record AdminBlogCommentReportResponse(
    Guid Id,
    BlogCommentReportReason Reason,
    string? Note,
    BlogCommentReportStatus Status,
    DateTimeOffset ReportedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>One comment for the admin, with its reports and — for a reply — the root it answers.</summary>
public sealed record AdminBlogCommentResponse(
    AdminBlogCommentListItemResponse Comment,
    string? ParentContent,
    IReadOnlyList<AdminBlogCommentReportResponse> Reports);
