namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// What the admin decided about a report. <see cref="Dismissed"/> leaves the review as it is;
/// the other two send it back to <see cref="ReviewModerationStatus.Rejected"/> with a reason —
/// the difference between them is the message to the author (fix this / this is gone), which
/// the reason text carries, not a fourth moderation status.
/// </summary>
public enum ReviewReportResolution
{
    Dismissed,
    ChangesRequested,
    Removed
}
