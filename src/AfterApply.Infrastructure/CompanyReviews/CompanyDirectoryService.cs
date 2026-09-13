using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>The anonymous side. Every query here starts from
/// <c>Status == Approved</c> — the filter is in the query, not in a mapper that could be bypassed.</summary>
internal sealed class CompanyDirectoryService(
    AppDbContext dbContext,
    CompanyReviewQueries queries,
    HybridCache cache,
    IOptions<CompanyReviewOptions> options) : ICompanyDirectoryService
{
    private const string ReviewedSlugsCacheKey = "company-reviews:reviewed-slugs";

    private static readonly HybridCacheEntryOptions ReviewedSlugsCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<PagedResult<CompanyPublicListItemResponse>> ListAsync(PublicCompanyListQuery query, CancellationToken cancellationToken)
    {
        var approved = dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved);

        // Only companies somebody has reviewed: the directory is a list of pages worth opening,
        // not a dump of every name anyone ever typed into an application form. Correlated
        // subqueries rather than GroupBy+Join, which EF Core cannot translate here.
        var companies = dbContext.Companies.Where(c => c.Slug != null && approved.Any(r => r.CompanyId == c.Id));
        var q = query.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var pattern = $"%{TurkishTextNormalizer.FoldCase(q).ToUpperInvariant()}%";
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern));
        }

        var joined = companies.Select(c => new
        {
            c.Id,
            Slug = c.Slug!,
            c.Name,
            Count = approved.Count(r => r.CompanyId == c.Id),
            Sum = approved.Where(r => r.CompanyId == c.Id).Sum(r => r.OverallRating)
        });

        var total = await joined.CountAsync(cancellationToken);
        var pageSize = options.Value.PublicPageSize;
        var rows = await joined
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var opts = options.Value;
        var globalAverage = rows.Any(r => r.Count >= opts.MinimumReviewsForScore)
            ? await queries.GetGlobalAverageAsync(cancellationToken)
            : CompanyReviewScoring.NeutralAverage;

        var items = rows.Select(r => new CompanyPublicListItemResponse(r.Id, r.Slug, r.Name, r.Count,
                CompanyReviewScoring.BayesianScore(r.Count, r.Sum, globalAverage, opts.PriorWeight, opts.MinimumReviewsForScore)))
            .ToList();

        return new PagedResult<CompanyPublicListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<CompanyPublicResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .Where(c => c.Slug == slug)
            .Select(c => new { c.Id, c.Name, c.Website })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return null;
        }

        var summary = await queries.GetSummaryAsync(company.Id, cancellationToken);
        return new CompanyPublicResponse(company.Id, slug, company.Name, company.Website, summary);
    }

    public async Task<PagedResult<CompanyReviewPublicResponse>?> ListApprovedReviewsAsync(string slug, PublicReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var companyId = await dbContext.Companies
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyId is null)
        {
            return null;
        }

        var approved = dbContext.CompanyReviews
            .Where(r => r.CompanyId == companyId && r.Status == ReviewModerationStatus.Approved);

        var total = await approved.CountAsync(cancellationToken);
        var pageSize = options.Value.PublicPageSize;

        var items = await queries.ProjectPublicAsync(
            queries.OrderForPublic(approved, query.Sort)
                .ThenBy(r => r.Id)
                .Skip((query.Page - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);

        return new PagedResult<CompanyReviewPublicResponse>(items, total, query.Page, pageSize);
    }

    public async Task<IReadOnlyList<ReviewedCompanySlugResponse>> ListReviewedSlugsAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(ReviewedSlugsCacheKey, async ct =>
        {
            var approved = dbContext.CompanyReviews
                .Where(r => r.Status == ReviewModerationStatus.Approved && r.ModeratedAt != null);
            var rows = await dbContext.Companies
                .Where(c => c.Slug != null && approved.Any(r => r.CompanyId == c.Id))
                .OrderBy(c => c.Slug)
                .Select(c => new ReviewedCompanySlugResponse(
                    c.Slug!,
                    approved.Where(r => r.CompanyId == c.Id).Max(r => r.ModeratedAt!.Value)))
                .ToListAsync(ct);
            return (IReadOnlyList<ReviewedCompanySlugResponse>)rows;
        }, ReviewedSlugsCacheOptions, cancellationToken: cancellationToken);
}
