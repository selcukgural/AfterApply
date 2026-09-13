using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// A reader flagging an approved review ("Bu yorumu değerlendir"). One open report per reader
/// per review; resolving it records the decision here, and the review itself changes only
/// through <see cref="CompanyReview.Reject"/> when the resolution says so.
/// </summary>
public sealed class CompanyReviewReport : AuditableEntity
{
    public const int MaxNoteLength = 500;
    public const int MaxResolutionReasonLength = 500;

    public Guid ReviewId { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public ReviewReportReason Reason { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    public ReviewReportStatus Status { get; private set; }

    public ReviewReportResolution? Resolution { get; private set; }

    /// <summary>What the admin wrote when resolving. Required unless the report was dismissed —
    /// it becomes the review's rejection reason, which the author reads.</summary>
    public string? ResolutionReason { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Audit only, no foreign key — same reasoning as <see cref="CompanyReview.ModeratedByUserId"/>.</summary>
    public Guid? ResolvedByUserId { get; private set; }

    private CompanyReviewReport()
    {
    }

    public static CompanyReviewReport Create(Guid reviewId, Guid reporterUserId, ReviewReportReason reason,
        string? note, DateTimeOffset now)
    {
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reason == ReviewReportReason.Other && trimmedNote is null)
        {
            throw new ReviewReportNoteRequiredException();
        }

        return new CompanyReviewReport
        {
            ReviewId = reviewId,
            ReporterUserId = reporterUserId,
            Reason = reason,
            Note = trimmedNote,
            ReportedAt = now,
            Status = ReviewReportStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Resolve(Guid adminUserId, ReviewReportResolution resolution, string? reason, DateTimeOffset now)
    {
        if (Status == ReviewReportStatus.Resolved)
        {
            throw new ReviewReportAlreadyResolvedException();
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (resolution != ReviewReportResolution.Dismissed && trimmedReason is null)
        {
            throw new ReviewReportResolutionReasonRequiredException();
        }

        Status = ReviewReportStatus.Resolved;
        Resolution = resolution;
        ResolutionReason = trimmedReason;
        ResolvedAt = now;
        ResolvedByUserId = adminUserId;
        Touch(now);
    }
}

public sealed class ReviewReportNoteRequiredException()
    : DomainException("COMPANY_REVIEW_REPORT_NOTE_REQUIRED", "A report with reason 'Other' needs a note.");

public sealed class ReviewReportAlreadyResolvedException()
    : DomainException("COMPANY_REVIEW_REPORT_ALREADY_RESOLVED", "This report has already been resolved.");

public sealed class ReviewReportResolutionReasonRequiredException()
    : DomainException("COMPANY_REVIEW_REPORT_REASON_REQUIRED", "Requesting changes or removing a review requires a reason.");
