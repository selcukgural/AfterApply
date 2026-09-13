using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Application.CompanyReviews.Contracts;

// Three shapes, never mixed: what anyone on the internet sees (no author, month-precision
// dates), what the author sees of their own rows (status, rejection reason), and what an admin
// sees (the account behind a review). A public record must not gain an author field "because it
// is handy" — the anonymity promise on the privacy page is exactly this file.

/// <summary>The aggregate a public company page shows. Every average is null until at least one
/// approved review exists; <see cref="Score"/> stays null until <see cref="MinimumForScore"/>.</summary>
public sealed record CompanyReviewSummaryResponse(
    int ApprovedCount,
    double? Score,
    int MinimumForScore,
    int PriorWeight,
    double? AverageOverall,
    double? AverageManagement,
    double? AverageWorkEnvironment,
    double? AverageSalaryAndBenefits,
    double? AverageCareerAndDevelopment,
    /// <summary>How many approved reviews gave each Overall star, index 0 = 1 star.</summary>
    IReadOnlyList<int> Distribution);

public sealed record CompanyPublicResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Website,
    CompanyReviewSummaryResponse Summary);

public sealed record CompanyPublicListItemResponse(
    Guid Id,
    string Slug,
    string Name,
    int ApprovedCount,
    double? Score);

/// <summary>Month precision on <see cref="SubmittedMonth"/> (<c>yyyy-MM</c>) is deliberate: an
/// exact timestamp next to "former employee" is one more bit that narrows down who wrote it.</summary>
public sealed record CompanyReviewPublicResponse(
    Guid Id,
    string Title,
    string Pros,
    string Cons,
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating,
    string SubmittedMonth,
    int HelpfulCount);

/// <summary>For the sitemap: which company pages are worth a crawler's visit.</summary>
public sealed record ReviewedCompanySlugResponse(string Slug, DateTimeOffset LastApprovedAt);

public sealed record ResolvedCompanyResponse(Guid Id, string Slug, string Name);

public sealed record MyCompanyReviewResponse(
    Guid Id,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating,
    ReviewModerationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ModeratedAt);

public sealed record ReviewQuotaResponse(int Used, int Limit);

public sealed record MyReviewsResponse(IReadOnlyList<MyCompanyReviewResponse> Items, ReviewQuotaResponse Quota);

/// <summary>What a signed-in reader needs on top of the public page: their own review of this
/// company (any status), which reviews they already marked helpful, and how much quota is left.</summary>
public sealed record CompanyReviewViewerStateResponse(
    MyCompanyReviewResponse? OwnReview,
    IReadOnlyList<Guid> HelpfulMarkedReviewIds,
    ReviewQuotaResponse Quota);

public sealed record HelpfulToggleResponse(bool Marked, int HelpfulCount);

public sealed record ReportCompanyReviewResponse(Guid Id, DateTimeOffset ReportedAt);

// Admin shapes.

public sealed record AdminCompanyReviewListItemResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    string Title,
    int OverallRating,
    EmploymentStatus EmploymentStatus,
    ReviewModerationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ModeratedAt,
    int OpenReportCount);

public sealed record AdminCompanyReviewResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    Guid AuthorUserId,
    string AuthorEmail,
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating,
    ReviewModerationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ModeratedAt,
    int HelpfulCount,
    IReadOnlyList<AdminReviewReportResponse> Reports);

public sealed record AdminReviewReportResponse(
    Guid Id,
    Guid ReviewId,
    string ReviewTitle,
    Guid CompanyId,
    string CompanyName,
    Guid ReporterUserId,
    string ReporterEmail,
    ReviewReportReason Reason,
    string? Note,
    ReviewReportStatus Status,
    ReviewReportResolution? Resolution,
    string? ResolutionReason,
    DateTimeOffset ReportedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ModerationCountsResponse(int PendingReviews, int OpenReports);

public sealed record UserReviewQuotaResponse(Guid UserId, int? ReviewQuotaOverride, int EffectiveLimit, int Used);
