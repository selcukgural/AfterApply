using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Application.CompanyReviews.Contracts;

/// <summary>What the author writes. The company comes from the route, never from the body.</summary>
public sealed record CreateCompanyReviewRequest(
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating);

public sealed record UpdateCompanyReviewRequest(
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating);

/// <summary>Find-or-create a company by name so it can be reviewed — the same resolver the
/// application form uses, so "Türk Telekom A.Ş." and "turk telekom" land on one row.</summary>
public sealed record ResolveCompanyRequest(string Name);

public sealed record ReportCompanyReviewRequest(ReviewReportReason Reason, string? Note = null);

public sealed record RejectCompanyReviewRequest(string Reason);

public sealed record ResolveReviewReportRequest(ReviewReportResolution Resolution, string? Reason = null);

/// <summary>Null clears the override and the global default applies again.</summary>
public sealed record SetReviewQuotaRequest(int? ReviewQuotaOverride);

public sealed record PublicCompanyListQuery(string? Q = null, int Page = 1);

public enum PublicReviewSort
{
    Newest,
    MostHelpful
}

public sealed record PublicReviewListQuery(int Page = 1, PublicReviewSort Sort = PublicReviewSort.Newest);

public sealed record AdminReviewListQuery(
    ReviewModerationStatus? Status = null,
    string? Company = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1);

public sealed record AdminReportListQuery(ReviewReportStatus Status = ReviewReportStatus.Open, int Page = 1);
