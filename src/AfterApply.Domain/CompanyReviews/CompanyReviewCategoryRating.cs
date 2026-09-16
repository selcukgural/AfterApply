using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>One optional category rating of a structured review. Absent row = the reviewer left
/// that category blank, which is different from any number and is why these are not columns.</summary>
public sealed class CompanyReviewCategoryRating : Entity
{
    public Guid ReviewId { get; private set; }

    public ReviewCategory Category { get; private set; }

    public int Rating { get; private set; }

    private CompanyReviewCategoryRating()
    {
    }

    public static CompanyReviewCategoryRating Create(Guid reviewId, ReviewCategory category, int rating) =>
        new() { ReviewId = reviewId, Category = category, Rating = rating };
}
