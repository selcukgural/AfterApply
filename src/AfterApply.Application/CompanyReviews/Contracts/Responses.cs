using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Application.CompanyReviews.Contracts;

// Three shapes, never mixed: what anyone on the internet sees (no author, month-precision
// dates), what the author sees of their own rows (status, rejection reason), and what an admin
// sees (the account behind a review). A public record must not gain an author field "because it
// is handy" — the anonymity promise on the privacy page is exactly this file.

/// <summary>The aggregate a public company page shows. <see cref="AverageOverall"/> is null until
/// at least one approved review exists; <see cref="Score"/>, every category average and the two
/// "most picked" lists stay null/empty below <see cref="MinimumForScore"/> — a number nobody can
/// hide behind is a number one person wrote.</summary>
public sealed record CompanyReviewSummaryResponse(
    int ApprovedCount,
    double? Score,
    int MinimumForScore,
    int PriorWeight,
    double? AverageOverall,
    /// <summary>Always the ten optional categories, in <see cref="ReviewCategory"/> order.</summary>
    IReadOnlyList<ReviewCategoryAverageResponse> Categories,
    /// <summary>How many approved reviews gave each Overall star, index 0 = 1 star.</summary>
    IReadOnlyList<int> Distribution,
    IReadOnlyList<ReviewStatementCountResponse> TopLiked,
    IReadOnlyList<ReviewStatementCountResponse> TopImprovable);

/// <summary><see cref="Count"/> is how many approved reviews rated this category (legacy rows
/// count toward the three categories they map to); <see cref="Average"/> is null under the
/// threshold so the page can say "2 votes" instead of a number.</summary>
public sealed record ReviewCategoryAverageResponse(ReviewCategory Category, int Count, double? Average);

public sealed record ReviewStatementCountResponse(string Key, int Count);

public sealed record CompanyPublicResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Website,
    CompanyReviewSummaryResponse Summary,
    /// <summary>How many salary entries readers would find behind sign-in. Zero while the salary
    /// feature is off. Trailing and defaulted: older clients never see it.</summary>
    int SalaryCount = 0,
    /// <summary>How many candidate experiences the third tab holds. Zero while that feature is
    /// off. Trailing and defaulted, like <see cref="SalaryCount"/>.</summary>
    int CandidateExperienceCount = 0);

/// <summary>One directory card. A company is on the list once it has any published
/// contribution — a review, a salary entry or a candidate experience — and the three counts say
/// which; <see cref="Score"/> comes from reviews alone. The two trailing counts are zero while
/// their feature is off, and defaulted so older clients never see them.</summary>
public sealed record CompanyPublicListItemResponse(
    Guid Id,
    string Slug,
    string Name,
    int ApprovedCount,
    double? Score,
    int SalaryCount = 0,
    int CandidateExperienceCount = 0);

/// <summary>
/// Month precision on <see cref="SubmittedMonth"/> (<c>yyyy-MM</c>) is deliberate: an exact
/// timestamp next to "former employee" is one more bit that narrows down who wrote it.
///
/// No free text, by design (2026-09-16): a legacy row's title, pros and cons are not on this
/// record at all, so "not shown" is a property of the wire and not of one renderer. For a legacy
/// row <see cref="CategoryRatings"/> carries the three fixed ratings that map onto a current
/// category, and <see cref="LegacySalaryAndBenefitsRating"/> the fourth, which maps onto none.
/// </summary>
public sealed record CompanyReviewPublicResponse(
    Guid Id,
    ReviewFormat Format,
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    IReadOnlyList<ReviewCategoryRatingDto> CategoryRatings,
    int? LegacySalaryAndBenefitsRating,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    string SubmittedMonth,
    int HelpfulCount);

/// <summary>For the sitemap: which company pages are worth a crawler's visit — those with a
/// published review or a public candidate experience (salaries sit behind sign-in, so they do not
/// count). <see cref="LastApprovedAt"/> is the later of the two.</summary>
public sealed record ReviewedCompanySlugResponse(string Slug, DateTimeOffset LastApprovedAt);

public sealed record ResolvedCompanyResponse(Guid Id, string Slug, string Name);

/// <summary>The author's view of their own row. The legacy text fields are null on a structured
/// review and still present on a legacy one — the author may read what they wrote until they
/// convert it by editing.</summary>
public sealed record MyCompanyReviewResponse(
    Guid Id,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    ReviewFormat Format,
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    IReadOnlyList<ReviewCategoryRatingDto> CategoryRatings,
    int? LegacySalaryAndBenefitsRating,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    string? Title,
    string? Pros,
    string? Cons,
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
    ReviewFormat Format,
    string? Title,
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
    ReviewFormat Format,
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    IReadOnlyList<ReviewCategoryRatingDto> CategoryRatings,
    int? LegacySalaryAndBenefitsRating,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    string? Title,
    string? Pros,
    string? Cons,
    ReviewModerationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ModeratedAt,
    int HelpfulCount,
    IReadOnlyList<AdminReviewReportResponse> Reports);

public sealed record AdminReviewReportResponse(
    Guid Id,
    Guid ReviewId,
    ReviewFormat ReviewFormat,
    string? ReviewTitle,
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
