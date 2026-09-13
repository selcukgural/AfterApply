using AfterApply.Application.Applications;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.CompanyReviews;

internal sealed class CompanyReviewService(
    AppDbContext dbContext,
    ICompanyResolver companyResolver,
    CompanySlugAllocator slugAllocator,
    CompanyReviewQueries queries) : ICompanyReviewService
{
    public async Task<ResolvedCompanyResponse> ResolveCompanyAsync(Guid userId, string name, CancellationToken cancellationToken)
    {
        var companyId = await companyResolver.ResolveOrCreateAsync(name.Trim(), cancellationToken);
        var company = await dbContext.Companies.SingleAsync(c => c.Id == companyId, cancellationToken);
        await EnsureSlugAsync(company, cancellationToken);
        return new ResolvedCompanyResponse(company.Id, company.Slug!, company.Name);
    }

    public async Task<CompanyReviewViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        var own = (await queries.ProjectMineAsync(
            dbContext.CompanyReviews.Where(r => r.UserId == userId && r.CompanyId == companyId), cancellationToken)).FirstOrDefault();

        var marked = await dbContext.CompanyReviewHelpfulMarks
            .Where(m => m.UserId == userId)
            .Join(dbContext.CompanyReviews.Where(r => r.CompanyId == companyId), m => m.ReviewId, r => r.Id, (m, _) => m.ReviewId)
            .ToListAsync(cancellationToken);

        return new CompanyReviewViewerStateResponse(own, marked, await queries.GetQuotaAsync(userId, cancellationToken));
    }

    public async Task<MyCompanyReviewResponse?> CreateAsync(Guid userId, Guid companyId, CreateCompanyReviewRequest request,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null)
        {
            return null;
        }

        // Checked before the insert so the user gets the right message; the unique index below
        // is what actually guarantees it against two requests racing.
        if (await dbContext.CompanyReviews.AnyAsync(r => r.UserId == userId && r.CompanyId == companyId, cancellationToken))
        {
            throw new CompanyReviewAlreadyExistsException();
        }

        var quota = await queries.GetQuotaAsync(userId, cancellationToken);
        if (quota.Used >= quota.Limit)
        {
            throw new CompanyReviewQuotaReachedException(quota.Limit);
        }

        // A company created by an old instance during a rollout can still carry no slug; the
        // public page this review will appear on needs one.
        await EnsureSlugAsync(company, cancellationToken);

        var review = CompanyReview.Create(userId, companyId, CompanyReviewQueries.ToContent(request), DateTimeOffset.UtcNow);
        dbContext.CompanyReviews.Add(review);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanyReviewAlreadyExistsException();
        }

        return (await queries.ProjectMineAsync(dbContext.CompanyReviews.Where(r => r.Id == review.Id), cancellationToken)).Single();
    }

    public async Task<MyCompanyReviewResponse?> UpdateAsync(Guid userId, Guid reviewId, UpdateCompanyReviewRequest request,
        CancellationToken cancellationToken)
    {
        // Filtered on the caller: someone else's review is "not found", never "forbidden".
        var review = await dbContext.CompanyReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId, cancellationToken);
        if (review is null)
        {
            return null;
        }

        var wasApproved = review.Status == ReviewModerationStatus.Approved;
        review.Edit(CompanyReviewQueries.ToContent(request), DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasApproved)
        {
            // It just left the public page; the cached aggregate still counts it.
            await queries.EvictSummaryAsync(review.CompanyId, cancellationToken);
        }

        return (await queries.ProjectMineAsync(dbContext.CompanyReviews.Where(r => r.Id == review.Id), cancellationToken)).Single();
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await dbContext.CompanyReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId, cancellationToken);
        if (review is null)
        {
            return false;
        }

        dbContext.CompanyReviews.Remove(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        await queries.EvictSummaryAsync(review.CompanyId, cancellationToken);
        return true;
    }

    public async Task<MyReviewsResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await queries.ProjectMineAsync(
            dbContext.CompanyReviews.Where(r => r.UserId == userId).OrderByDescending(r => r.SubmittedAt), cancellationToken);
        return new MyReviewsResponse(items, await queries.GetQuotaAsync(userId, cancellationToken));
    }

    public async Task<HelpfulToggleResponse?> ToggleHelpfulAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await dbContext.CompanyReviews
            .Where(r => r.Id == reviewId)
            .Select(r => new { r.UserId, r.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (review is null || review.Status != ReviewModerationStatus.Approved)
        {
            // An unapproved review is not on any page, so "not found" is what the caller can see.
            return null;
        }

        if (review.UserId == userId)
        {
            throw new CompanyReviewNotMarkableException();
        }

        var existing = await dbContext.CompanyReviewHelpfulMarks
            .FirstOrDefaultAsync(m => m.ReviewId == reviewId && m.UserId == userId, cancellationToken);
        bool marked;
        if (existing is null)
        {
            dbContext.CompanyReviewHelpfulMarks.Add(CompanyReviewHelpfulMark.Create(reviewId, userId, DateTimeOffset.UtcNow));
            marked = true;
        }
        else
        {
            dbContext.CompanyReviewHelpfulMarks.Remove(existing);
            marked = false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Double-click: the other request already marked it. Same end state, report it.
            marked = true;
        }

        var count = await dbContext.CompanyReviewHelpfulMarks.CountAsync(m => m.ReviewId == reviewId, cancellationToken);
        return new HelpfulToggleResponse(marked, count);
    }

    public async Task<ReportCompanyReviewResponse?> ReportAsync(Guid userId, Guid reviewId, ReportCompanyReviewRequest request,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.CompanyReviews
            .Where(r => r.Id == reviewId)
            .Select(r => new { r.UserId, r.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (review is null || review.Status != ReviewModerationStatus.Approved)
        {
            return null;
        }

        if (review.UserId == userId)
        {
            throw new CompanyReviewNotReportableException();
        }

        if (await dbContext.CompanyReviewReports.AnyAsync(
                x => x.ReviewId == reviewId && x.ReporterUserId == userId && x.Status == ReviewReportStatus.Open,
                cancellationToken))
        {
            throw new CompanyReviewReportAlreadyOpenException();
        }

        var report = CompanyReviewReport.Create(reviewId, userId, request.Reason, request.Note, DateTimeOffset.UtcNow);
        dbContext.CompanyReviewReports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanyReviewReportAlreadyOpenException();
        }

        return new ReportCompanyReviewResponse(report.Id, report.ReportedAt);
    }

    private async Task EnsureSlugAsync(Domain.Companies.Company company, CancellationToken cancellationToken)
    {
        if (company.Slug is not null)
        {
            return;
        }

        company.AssignSlug(await slugAllocator.AllocateAsync(company.Name, cancellationToken), DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
