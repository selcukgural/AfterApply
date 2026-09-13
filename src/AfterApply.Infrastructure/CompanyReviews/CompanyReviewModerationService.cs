using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>Admin only — the caller has already passed <c>IAdminAccessService</c>. This is the one
/// place a review's author is joined to a response.</summary>
internal sealed class CompanyReviewModerationService(
    AppDbContext dbContext,
    CompanyReviewQueries queries,
    IOptions<CompanyReviewOptions> options) : ICompanyReviewModerationService
{
    public async Task<PagedResult<AdminCompanyReviewListItemResponse>> ListAsync(AdminReviewListQuery query, CancellationToken cancellationToken)
    {
        var reviews = dbContext.CompanyReviews.AsQueryable();
        if (query.Status is { } status)
        {
            reviews = reviews.Where(r => r.Status == status);
        }

        if (query.From is { } from)
        {
            reviews = reviews.Where(r => r.SubmittedAt >= from);
        }

        if (query.To is { } to)
        {
            reviews = reviews.Where(r => r.SubmittedAt <= to);
        }

        var companies = dbContext.Companies.AsQueryable();
        var company = query.Company?.Trim();
        if (!string.IsNullOrEmpty(company))
        {
            var pattern = $"%{TurkishTextNormalizer.FoldCase(company).ToUpperInvariant()}%";
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern));
        }

        var joined = reviews.Join(companies, r => r.CompanyId, c => c.Id, (r, c) => new { r, c });
        var total = await joined.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;

        var items = await joined
            // Oldest pending first: the queue is worked in the order people submitted.
            .OrderBy(x => x.r.Status == ReviewModerationStatus.Pending ? 0 : 1)
            .ThenBy(x => x.r.SubmittedAt)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminCompanyReviewListItemResponse(
                x.r.Id, x.c.Id, x.c.Name, x.c.Slug, x.r.Title, x.r.OverallRating, x.r.EmploymentStatus,
                x.r.Status, x.r.SubmittedAt, x.r.ModeratedAt,
                dbContext.CompanyReviewReports.Count(p => p.ReviewId == x.r.Id && p.Status == ReviewReportStatus.Open)))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCompanyReviewListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<AdminCompanyReviewResponse?> GetAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var row = await dbContext.CompanyReviews
            .Where(r => r.Id == reviewId)
            .Join(dbContext.Companies, r => r.CompanyId, c => c.Id, (r, c) => new { r, c })
            .Join(dbContext.Users, x => x.r.UserId, u => u.Id, (x, u) => new { x.r, x.c, AuthorEmail = u.Email ?? string.Empty })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var helpful = await dbContext.CompanyReviewHelpfulMarks.CountAsync(m => m.ReviewId == reviewId, cancellationToken);
        var reports = await ProjectReportsAsync(
            dbContext.CompanyReviewReports.Where(p => p.ReviewId == reviewId).OrderByDescending(p => p.ReportedAt),
            cancellationToken);

        var r = row.r;
        return new AdminCompanyReviewResponse(r.Id, row.c.Id, row.c.Name, row.c.Slug, r.UserId, row.AuthorEmail,
            r.EmploymentStatus, r.Title, r.Pros, r.Cons, r.OverallRating, r.ManagementRating, r.WorkEnvironmentRating,
            r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating, r.Status, r.RejectionReason, r.SubmittedAt,
            r.ModeratedAt, helpful, reports);
    }

    public async Task<bool> ApproveAsync(Guid adminUserId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await dbContext.CompanyReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return false;
        }

        review.Approve(adminUserId, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await queries.EvictSummaryAsync(review.CompanyId, cancellationToken);
        return true;
    }

    public async Task<bool> RejectAsync(Guid adminUserId, Guid reviewId, string reason, CancellationToken cancellationToken)
    {
        var review = await dbContext.CompanyReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return false;
        }

        review.Reject(adminUserId, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await queries.EvictSummaryAsync(review.CompanyId, cancellationToken);
        return true;
    }

    public async Task<PagedResult<AdminReviewReportResponse>> ListReportsAsync(AdminReportListQuery query, CancellationToken cancellationToken)
    {
        var reports = dbContext.CompanyReviewReports.Where(p => p.Status == query.Status);
        var total = await reports.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;

        var items = await ProjectReportsAsync(
            reports.OrderBy(p => p.ReportedAt).Skip((query.Page - 1) * pageSize).Take(pageSize),
            cancellationToken);

        return new PagedResult<AdminReviewReportResponse>(items, total, query.Page, pageSize);
    }

    public async Task<bool> ResolveReportAsync(Guid adminUserId, Guid reportId, ResolveReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await dbContext.CompanyReviewReports.FirstOrDefaultAsync(p => p.Id == reportId, cancellationToken);
        if (report is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        report.Resolve(adminUserId, request.Resolution, request.Reason, now);

        if (request.Resolution != ReviewReportResolution.Dismissed)
        {
            var review = await dbContext.CompanyReviews.SingleAsync(r => r.Id == report.ReviewId, cancellationToken);
            review.Reject(adminUserId, request.Reason!, now);

            // Every other open report on the same review was about the same text, and that text
            // is now off the page — closing them with the same decision keeps the queue honest.
            var siblings = await dbContext.CompanyReviewReports
                .Where(p => p.ReviewId == report.ReviewId && p.Id != report.Id && p.Status == ReviewReportStatus.Open)
                .ToListAsync(cancellationToken);
            foreach (var sibling in siblings)
            {
                sibling.Resolve(adminUserId, request.Resolution, request.Reason, now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await queries.EvictSummaryAsync(review.CompanyId, cancellationToken);
            return true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ModerationCountsResponse> GetCountsAsync(CancellationToken cancellationToken)
    {
        var pending = await dbContext.CompanyReviews.CountAsync(r => r.Status == ReviewModerationStatus.Pending, cancellationToken);
        var open = await dbContext.CompanyReviewReports.CountAsync(p => p.Status == ReviewReportStatus.Open, cancellationToken);
        return new ModerationCountsResponse(pending, open);
    }

    public async Task<UserReviewQuotaResponse?> SetUserQuotaAsync(Guid userId, int? reviewQuotaOverride, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.ReviewQuotaOverride = reviewQuotaOverride;
        await dbContext.SaveChangesAsync(cancellationToken);

        var used = await queries.CountUserReviewsAsync(userId, cancellationToken);
        return new UserReviewQuotaResponse(userId, reviewQuotaOverride, reviewQuotaOverride ?? options.Value.MaxReviewsPerUser, used);
    }

    private async Task<List<AdminReviewReportResponse>> ProjectReportsAsync(IQueryable<CompanyReviewReport> reports,
        CancellationToken cancellationToken)
    {
        var rows = await reports
            .Join(dbContext.CompanyReviews, p => p.ReviewId, r => r.Id, (p, r) => new { p, r })
            .Join(dbContext.Companies, x => x.r.CompanyId, c => c.Id, (x, c) => new { x.p, x.r, c })
            .Join(dbContext.Users, x => x.p.ReporterUserId, u => u.Id, (x, u) => new
            {
                Report = x.p, ReviewTitle = x.r.Title, ReviewId = x.r.Id, CompanyId = x.c.Id, CompanyName = x.c.Name,
                ReporterEmail = u.Email ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new AdminReviewReportResponse(
            x.Report.Id, x.ReviewId, x.ReviewTitle, x.CompanyId, x.CompanyName, x.Report.ReporterUserId, x.ReporterEmail,
            x.Report.Reason, x.Report.Note, x.Report.Status, x.Report.Resolution, x.Report.ResolutionReason,
            x.Report.ReportedAt, x.Report.ResolvedAt)).ToList();
    }
}
