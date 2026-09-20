using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>
/// A reader's comment on a published post (DECISIONS.md 2026-09-20). Plain text — never HTML,
/// never rendered as markup — one level of replies (<see cref="ParentCommentId"/> is always a root
/// comment's id), and moderation before the page: every comment starts <see cref="BlogCommentStatus.Pending"/>
/// unless an admin wrote it. The author may edit it while it is pending and never after — a
/// comment that has been on the site stays what other readers replied to. Nothing is ever
/// deleted by its author (decided 2026-09-20); the row goes with the account, the post, or its
/// parent comment.
/// </summary>
public sealed class BlogComment : AuditableEntity
{
    public const int MinContentLength = 10;
    public const int MaxContentLength = 3000;

    public Guid PostId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>The root comment this one answers, or null for a root comment. Never a reply's
    /// id — the service resolves a reply-to-a-reply to the root before it gets here.</summary>
    public Guid? ParentCommentId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public BlogCommentStatus Status { get; private set; }

    /// <summary>Set on the first edit and every one after; null for a comment as first written.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset? ModeratedAt { get; private set; }

    /// <summary>Audit only, no foreign key — the same reasoning as <c>CompanyReview.ModeratedByUserId</c>.</summary>
    public Guid? ModeratedByUserId { get; private set; }

    private BlogComment()
    {
    }

    public static BlogComment Create(Guid postId, Guid userId, Guid? parentCommentId, string content, bool approved,
        DateTimeOffset now)
    {
        var comment = new BlogComment
        {
            PostId = postId,
            UserId = userId,
            ParentCommentId = parentCommentId,
            Content = Normalize(content),
            Status = approved ? BlogCommentStatus.Approved : BlogCommentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (approved)
        {
            comment.ModeratedAt = now;
            comment.ModeratedByUserId = userId;
        }

        return comment;
    }

    /// <summary>The author's edit. Only while pending: once approved the text is what other
    /// readers saw and replied to, and once rejected there is nothing to fix.</summary>
    /// <exception cref="BlogCommentLockedException">The comment is no longer pending.</exception>
    public void Edit(string content, DateTimeOffset now)
    {
        if (Status != BlogCommentStatus.Pending)
        {
            throw new BlogCommentLockedException();
        }

        var normalized = Normalize(content);
        if (normalized == Content)
        {
            return;
        }

        Content = normalized;
        EditedAt = now;
        Touch(now);
    }

    public void Approve(Guid adminUserId, DateTimeOffset now) => Moderate(BlogCommentStatus.Approved, adminUserId, now);

    public void Reject(Guid adminUserId, DateTimeOffset now) => Moderate(BlogCommentStatus.Rejected, adminUserId, now);

    private void Moderate(BlogCommentStatus status, Guid adminUserId, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        ModeratedAt = now;
        ModeratedByUserId = adminUserId;
        Touch(now);
    }

    /// <summary>Trimmed, line endings unified — what is stored and what the duplicate check
    /// compares. The length rules are the validator's; this only refuses what no validator saw
    /// (a direct domain call).</summary>
    public static string Normalize(string content)
    {
        var normalized = (content ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (normalized.Length is < MinContentLength or > MaxContentLength)
        {
            throw new ArgumentOutOfRangeException(nameof(content), normalized.Length,
                $"A comment is {MinContentLength} to {MaxContentLength} characters.");
        }

        return normalized;
    }
}
