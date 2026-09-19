using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>A reader's like on a published post. One per reader per post (unique index);
/// toggling off deletes the row rather than flagging it, so the count is a plain COUNT — the
/// <c>CompanyReviewHelpfulMark</c> shape.</summary>
public sealed class BlogPostLike : Entity
{
    public Guid PostId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset LikedAt { get; private set; }

    private BlogPostLike()
    {
    }

    public static BlogPostLike Create(Guid postId, Guid userId, DateTimeOffset now) =>
        new() { PostId = postId, UserId = userId, LikedAt = now };
}
