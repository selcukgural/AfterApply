namespace AfterApply.Domain.CompanyReviews;

/// <summary>The reviewer's relationship to the company at the time of writing. Shown next to
/// the review; it is the one fact about the author a reader gets.</summary>
public enum EmploymentStatus
{
    CurrentEmployee,
    FormerEmployee,
    Intern
}
