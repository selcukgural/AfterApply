using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// One user's review of one company — anonymous to readers, attributed to an account in the
/// database. The <c>(UserId, CompanyId)</c> pair is unique: a user gets one voice per company
/// and edits it rather than adding to it, which is what keeps a score from being one person
/// typed five times.
///
/// Nothing here is public until an admin approves it, and an edit takes it out of public view
/// again (<see cref="Edit"/>). The moderation fields record who decided and when; the author's
/// identity is exposed only on the admin surface.
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

    public EmploymentStatus EmploymentStatus { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Pros { get; private set; } = string.Empty;

    public string Cons { get; private set; } = string.Empty;

    public int OverallRating { get; private set; }

    public int ManagementRating { get; private set; }

    public int WorkEnvironmentRating { get; private set; }

    public int SalaryAndBenefitsRating { get; private set; }

    public int CareerAndDevelopmentRating { get; private set; }

    public ReviewModerationStatus Status { get; private set; }

    /// <summary>When the current text was submitted — reset on every edit, so the public
    /// "September 2026" label describes the words on screen, not the first draft.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? ModeratedAt { get; private set; }

    /// <summary>Audit only, no foreign key: an admin account being deleted must not erase the
    /// record that a decision was taken.</summary>
    public Guid? ModeratedByUserId { get; private set; }

    /// <summary>Shown to the author on their own list, never to the public.</summary>
    public string? RejectionReason { get; private set; }

    private CompanyReview()
    {
    }

    public static CompanyReview Create(Guid userId, Guid companyId, ReviewContent content, DateTimeOffset now)
    {
        content.Validate();

        var review = new CompanyReview
        {
            UserId = userId,
            CompanyId = companyId,
            Status = ReviewModerationStatus.Pending,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        review.Apply(content);
        return review;
    }

    /// <summary>Replaces the content and sends the review back to moderation. Also clears the
    /// previous decision: a rejection reason describes text that no longer exists.</summary>
    public void Edit(ReviewContent content, DateTimeOffset now)
    {
        content.Validate();
        Apply(content);
        Status = ReviewModerationStatus.Pending;
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

    private void Apply(ReviewContent content)
    {
        EmploymentStatus = content.EmploymentStatus;
        Title = content.Title.Trim();
        Pros = content.Pros.Trim();
        Cons = content.Cons.Trim();
        OverallRating = content.OverallRating;
        ManagementRating = content.ManagementRating;
        WorkEnvironmentRating = content.WorkEnvironmentRating;
        SalaryAndBenefitsRating = content.SalaryAndBenefitsRating;
        CareerAndDevelopmentRating = content.CareerAndDevelopmentRating;
    }
}

/// <summary>Everything the author writes, in one value so Create and Edit share one invariant.</summary>
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
    /// <summary>The request validator says the same things earlier and in the user's language;
    /// this is the boundary that stores the row, so it checks again.</summary>
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

public sealed class ReviewContentInvalidException()
    : DomainException("COMPANY_REVIEW_CONTENT_INVALID", "Review text must be present and every rating between 1 and 5.");

public sealed class ReviewRejectionReasonRequiredException()
    : DomainException("COMPANY_REVIEW_REJECTION_REASON_REQUIRED", "Rejecting a review requires a reason.");
