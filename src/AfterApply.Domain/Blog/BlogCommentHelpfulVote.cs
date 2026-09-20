using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>A reader finding a comment helpful. One per reader per comment (unique index);
/// taking it back deletes the row, so the count is a plain COUNT — the <see cref="BlogPostLike"/> shape.</summary>
public sealed class BlogCommentHelpfulVote : Entity
{
    public Guid CommentId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset VotedAt { get; private set; }

    private BlogCommentHelpfulVote()
    {
    }

    public static BlogCommentHelpfulVote Create(Guid commentId, Guid userId, DateTimeOffset now) =>
        new() { CommentId = commentId, UserId = userId, VotedAt = now };
}
