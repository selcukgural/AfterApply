using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>One predefined statement a reviewer picked. Only the catalogue key is stored — the
/// wording is looked up at render time, so a legal revision of a sentence changes every review
/// that picked it without a data migration.</summary>
public sealed class CompanyReviewStatementPick : Entity
{
    public const int MaxKeyLength = 80;

    public Guid ReviewId { get; private set; }

    public string StatementKey { get; private set; } = string.Empty;

    public ReviewStatementKind Kind { get; private set; }

    private CompanyReviewStatementPick()
    {
    }

    public static CompanyReviewStatementPick Create(Guid reviewId, ReviewStatement statement) =>
        new() { ReviewId = reviewId, StatementKey = statement.Key, Kind = statement.Kind };
}
