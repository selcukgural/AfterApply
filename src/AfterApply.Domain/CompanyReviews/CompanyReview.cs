using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// One user's review of one company — anonymous to readers, attributed to an account in the
/// database. The <c>(UserId, CompanyId)</c> pair is unique: a user gets one voice per company
/// and edits it rather than adding to it, which is what keeps a score from being one person
/// typed five times.
///
/// Two shapes share this row (<see cref="Format"/>). A <see cref="ReviewFormat.Structured"/>
/// review is a required overall rating, optional per-category ratings and picks from the
/// statement catalogue — nothing the author typed, so it is published the moment it is saved and
/// moderated only through reports. A <see cref="ReviewFormat.Legacy"/> review is the pre-2026-09-16
/// shape with free text; those rows are kept untouched (the text is never nulled) but only their
/// ratings are shown publicly, and a human still reads the ones that were pending at the switch.
/// </summary>
public sealed class CompanyReview : AuditableEntity
{
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxRejectionReasonLength = 500;
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public ReviewFormat Format { get; private set; }

    public EmploymentStatus EmploymentStatus { get; private set; }

    /// <summary>Legacy free text. Null on structured rows; on a legacy row that was later
    /// converted the original stays in place — see <see cref="EditStructured"/>.</summary>
    public string? Title { get; private set; }

    public string? Pros { get; private set; }

    public string? Cons { get; private set; }

    /// <summary>The one rating every review has: it is the input to the company score.</summary>
    public int OverallRating { get; private set; }

    /// <summary>The four fixed categories of the legacy form. Null on structured rows, whose
    /// category ratings live in <c>CompanyReviewCategoryRatings</c>; kept here so old rows keep
    /// contributing to the matching category averages.</summary>
    public int? ManagementRating { get; private set; }

    public int? WorkEnvironmentRating { get; private set; }

    public int? SalaryAndBenefitsRating { get; private set; }

    public int? CareerAndDevelopmentRating { get; private set; }

    public ReviewModerationStatus Status { get; private set; }

    /// <summary>When the current content was submitted — reset on every edit, so the public
    /// "September 2026" label describes what is on screen, not the first draft.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Set only by a human decision. Null on an auto-published structured review.</summary>
    public DateTimeOffset? ModeratedAt { get; private set; }

    /// <summary>Audit only, no foreign key: an admin account being deleted must not erase the
    /// record that a decision was taken.</summary>
    public Guid? ModeratedByUserId { get; private set; }

    /// <summary>Shown to the author on their own list, never to the public.</summary>
    public string? RejectionReason { get; private set; }

    private CompanyReview()
    {
    }

    /// <summary>The pre-2026-09-16 shape, pending human moderation. No production path creates
    /// these any more; it exists so tests can seed the rows the migration left behind.</summary>
    public static CompanyReview CreateLegacy(Guid userId, Guid companyId, ReviewContent content, DateTimeOffset now)
    {
        content.Validate();

        var review = new CompanyReview
        {
            UserId = userId,
            CompanyId = companyId,
            Format = ReviewFormat.Legacy,
            Status = ReviewModerationStatus.Pending,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            EmploymentStatus = content.EmploymentStatus,
            Title = content.Title.Trim(),
            Pros = content.Pros.Trim(),
            Cons = content.Cons.Trim(),
            OverallRating = content.OverallRating,
            ManagementRating = content.ManagementRating,
            WorkEnvironmentRating = content.WorkEnvironmentRating,
            SalaryAndBenefitsRating = content.SalaryAndBenefitsRating,
            CareerAndDevelopmentRating = content.CareerAndDevelopmentRating
        };
        return review;
    }

    /// <summary>A structured review is public from the first save: there is nothing in it a
    /// moderator could read that the catalogue did not already approve.</summary>
    public static CompanyReview CreateStructured(Guid userId, Guid companyId, StructuredReviewContent content, DateTimeOffset now)
    {
        content.Validate();

        var review = new CompanyReview
        {
            UserId = userId,
            CompanyId = companyId,
            Format = ReviewFormat.Structured,
            Status = ReviewModerationStatus.Approved,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            EmploymentStatus = content.EmploymentStatus,
            OverallRating = content.OverallRating
        };
        return review;
    }

    /// <summary>
    /// Replaces the scalar content with the structured shape (the caller rewrites the child rows).
    /// A legacy row becomes structured here; its old title/pros/cons and fixed category ratings
    /// are deliberately left in place — nothing is ever erased from a review, only hidden. Readers
    /// of the legacy columns key off <see cref="Format"/>, so the stale values are inert.
    ///
    /// Stays published, with one exception: a review a moderator has rejected goes back to
    /// pending, so the decision cannot be undone by saving the same picks again.
    /// </summary>
    public void EditStructured(StructuredReviewContent content, DateTimeOffset now)
    {
        content.Validate();

        var wasRejected = Status == ReviewModerationStatus.Rejected;
        Format = ReviewFormat.Structured;
        EmploymentStatus = content.EmploymentStatus;
        OverallRating = content.OverallRating;
        Status = wasRejected ? ReviewModerationStatus.Pending : ReviewModerationStatus.Approved;
        SubmittedAt = now;
        ModeratedAt = null;
        ModeratedByUserId = null;
        RejectionReason = null;
        Touch(now);
    }

    public void Approve(Guid adminUserId, DateTimeOffset now)
    {
        Status = ReviewModerationStatus.Approved;
        ModeratedAt = now;
        ModeratedByUserId = adminUserId;
        RejectionReason = null;
        Touch(now);
    }

    public void Reject(Guid adminUserId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReviewRejectionReasonRequiredException();
        }

        Status = ReviewModerationStatus.Rejected;
        ModeratedAt = now;
        ModeratedByUserId = adminUserId;
        RejectionReason = reason.Trim();
        Touch(now);
    }
}

/// <summary>The legacy free-text shape. Only <see cref="CompanyReview.CreateLegacy"/> takes it.</summary>
public readonly record struct ReviewContent(
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Pros) || string.IsNullOrWhiteSpace(Cons))
        {
            throw new ReviewContentInvalidException();
        }

        foreach (var rating in new[] { OverallRating, ManagementRating, WorkEnvironmentRating, SalaryAndBenefitsRating, CareerAndDevelopmentRating })
        {
            if (rating is < CompanyReview.MinRating or > CompanyReview.MaxRating)
            {
                throw new ReviewContentInvalidException();
            }
        }
    }
}

public readonly record struct CategoryRating(ReviewCategory Category, int Rating);

/// <summary>
/// Everything a structured review says, in one value so Create and Edit share one invariant.
/// The request validator says the same things earlier and in the user's language; this is the
/// boundary that stores the row, so it checks again — in particular that every statement key is
/// in the catalogue, because a key that is not would render as nothing on every card.
/// </summary>
public readonly record struct StructuredReviewContent(
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    IReadOnlyList<CategoryRating> CategoryRatings,
    IReadOnlyList<string> Liked,
    IReadOnlyList<string> Improvable)
{
    public void Validate()
    {
        if (OverallRating is < CompanyReview.MinRating or > CompanyReview.MaxRating)
        {
            throw new ReviewContentInvalidException();
        }

        var seenCategories = new HashSet<ReviewCategory>();
        foreach (var (category, rating) in CategoryRatings)
        {
            // Overall is the column, never a child row.
            if (category == ReviewCategory.Overall || !Enum.IsDefined(category) || !seenCategories.Add(category)
                || rating is < CompanyReview.MinRating or > CompanyReview.MaxRating)
            {
                throw new ReviewContentInvalidException();
            }
        }

        ValidatePicks(Liked, ReviewStatementKind.Liked);
        ValidatePicks(Improvable, ReviewStatementKind.Improve);
    }

    /// <summary>The catalogue entries behind the picks, in the author's order.</summary>
    public IEnumerable<ReviewStatement> Statements()
    {
        foreach (var key in Liked.Concat(Improvable))
        {
            ReviewStatementCatalogue.TryGet(key, out var statement);
            yield return statement;
        }
    }

    private static void ValidatePicks(IReadOnlyList<string> keys, ReviewStatementKind kind)
    {
        if (keys.Count > ReviewStatementCatalogue.MaxPicksPerKind)
        {
            throw new ReviewContentInvalidException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!ReviewStatementCatalogue.TryGet(key, out var statement) || statement.Kind != kind || !seen.Add(key))
            {
                throw new ReviewContentInvalidException();
            }
        }
    }
}

public sealed class ReviewContentInvalidException()
    : DomainException("COMPANY_REVIEW_CONTENT_INVALID",
        "The overall rating must be 1–5, category ratings 1–5 without repeats, and statements at most 5 per kind from the catalogue.");

public sealed class ReviewRejectionReasonRequiredException()
    : DomainException("COMPANY_REVIEW_REJECTION_REASON_REQUIRED", "Rejecting a review requires a reason.");
