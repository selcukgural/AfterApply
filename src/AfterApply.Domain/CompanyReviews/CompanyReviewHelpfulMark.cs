using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>A reader's "helpful" on an approved review. One per reader per review (unique
/// index); toggling off deletes the row rather than flagging it, so the count is a plain COUNT.</summary>
public sealed class CompanyReviewHelpfulMark : Entity
{
    public Guid ReviewId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset MarkedAt { get; private set; }

    private CompanyReviewHelpfulMark()
    {
    }

    public static CompanyReviewHelpfulMark Create(Guid reviewId, Guid userId, DateTimeOffset now) =>
        new() { ReviewId = reviewId, UserId = userId, MarkedAt = now };
}
