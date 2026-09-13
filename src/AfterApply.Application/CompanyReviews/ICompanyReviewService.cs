using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Common;
using AfterApply.Application.CompanyReviews.Contracts;

namespace AfterApply.Application.CompanyReviews;

/// <summary>What a signed-in user does with reviews: their own, plus helpful marks and reports on
/// other people's. Every lookup of "their own" filters on the caller's id and answers null for
/// anyone else's row — a review that is not yours is "not found", never "forbidden".</summary>
public interface ICompanyReviewService
{
    Task<ResolvedCompanyResponse> ResolveCompanyAsync(Guid userId, string name, CancellationToken cancellationToken);

    /// <returns>Null when the company does not exist.</returns>
    Task<CompanyReviewViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    /// <exception cref="CompanyReviewQuotaReachedException">The account is at its review quota.</exception>
    /// <exception cref="CompanyReviewAlreadyExistsException">The account already reviewed this company.</exception>
    /// <returns>Null when the company does not exist.</returns>
    Task<MyCompanyReviewResponse?> CreateAsync(Guid userId, Guid companyId, CreateCompanyReviewRequest request,
        CancellationToken cancellationToken);

    Task<MyCompanyReviewResponse?> UpdateAsync(Guid userId, Guid reviewId, UpdateCompanyReviewRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken);

    Task<MyReviewsResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <exception cref="CompanyReviewNotMarkableException">Own review, or not approved.</exception>
    /// <returns>Null when there is no approved review with that id.</returns>
    Task<HelpfulToggleResponse?> ToggleHelpfulAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken);

    /// <exception cref="CompanyReviewNotReportableException">Own review, or not approved.</exception>
    /// <exception cref="CompanyReviewReportAlreadyOpenException">The caller already has an open report on it.</exception>
    /// <returns>Null when there is no approved review with that id.</returns>
    Task<ReportCompanyReviewResponse?> ReportAsync(Guid userId, Guid reviewId, ReportCompanyReviewRequest request,
        CancellationToken cancellationToken);
}

/// <summary>The anonymous read side: company pages and their approved reviews.</summary>
public interface ICompanyDirectoryService
{
    Task<PagedResult<CompanyPublicListItemResponse>> ListAsync(PublicCompanyListQuery query, CancellationToken cancellationToken);

    Task<CompanyPublicResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <returns>Null when there is no company with that slug.</returns>
    Task<PagedResult<CompanyReviewPublicResponse>?> ListApprovedReviewsAsync(string slug, PublicReviewListQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewedCompanySlugResponse>> ListReviewedSlugsAsync(CancellationToken cancellationToken);
}

public interface ICompanyReviewModerationService
{
    Task<PagedResult<AdminCompanyReviewListItemResponse>> ListAsync(AdminReviewListQuery query, CancellationToken cancellationToken);

    Task<AdminCompanyReviewResponse?> GetAsync(Guid reviewId, CancellationToken cancellationToken);

    Task<bool> ApproveAsync(Guid adminUserId, Guid reviewId, CancellationToken cancellationToken);

    Task<bool> RejectAsync(Guid adminUserId, Guid reviewId, string reason, CancellationToken cancellationToken);

    Task<PagedResult<AdminReviewReportResponse>> ListReportsAsync(AdminReportListQuery query, CancellationToken cancellationToken);

    Task<bool> ResolveReportAsync(Guid adminUserId, Guid reportId, ResolveReviewReportRequest request,
        CancellationToken cancellationToken);

    Task<ModerationCountsResponse> GetCountsAsync(CancellationToken cancellationToken);

    Task<UserReviewQuotaResponse?> SetUserQuotaAsync(Guid userId, int? reviewQuotaOverride, CancellationToken cancellationToken);
}

/// <summary>The account holds as many reviews as it is allowed to. {0} = the limit that applies to
/// this account (its override, or the global default).</summary>
public sealed class CompanyReviewQuotaReachedException(int limit)
    : CodedException("COMPANY_REVIEW_QUOTA_REACHED", $"A user may hold at most {limit} company reviews.", limit);

public sealed class CompanyReviewAlreadyExistsException()
    : CodedException("COMPANY_REVIEW_ALREADY_EXISTS", "This account has already reviewed this company; edit that review instead.");

public sealed class CompanyReviewNotMarkableException()
    : CodedException("COMPANY_REVIEW_NOT_MARKABLE", "Only another person's approved review can be marked helpful.");

public sealed class CompanyReviewNotReportableException()
    : CodedException("COMPANY_REVIEW_NOT_REPORTABLE", "Only another person's approved review can be reported.");

public sealed class CompanyReviewReportAlreadyOpenException()
    : CodedException("COMPANY_REVIEW_REPORT_ALREADY_OPEN", "This account already has an open report on this review.");
