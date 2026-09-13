namespace AfterApply.Domain.CompanyReviews;

/// <summary>
/// Every review starts <see cref="Pending"/> and goes back there on every edit. Only
/// <see cref="Approved"/> rows are ever read by a public query or counted in a score.
/// </summary>
public enum ReviewModerationStatus
{
    Pending,
    Approved,
    Rejected
}
