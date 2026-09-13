using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>
/// The pieces every review service needs: the site-wide average behind the Bayesian score, a
/// company's aggregate, and the projection of a review to its author-facing shape. Scoped, like
/// the services that use it, so it shares their DbContext.
/// </summary>
internal sealed class CompanyReviewQueries(AppDbContext dbContext, HybridCache cache, IOptions<CompanyReviewOptions> options)
{
    private const string GlobalAverageCacheKey = "company-reviews:global-average";

    private static readonly HybridCacheEntryOptions GlobalAverageCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private static readonly HybridCacheEntryOptions SummaryCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60)
    };

    public static string SummaryCacheKey(Guid companyId) => $"company-reviews:summary:{companyId}";

    /// <summary>Mean Overall rating over every approved review on the site — the prior the score
    /// pulls toward. Cached for five minutes: it moves by a rounding error per review, and every
    /// public page reads it. Falls back to the scale's midpoint while nothing is approved yet.</summary>
    public ValueTask<double> GetGlobalAverageAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(GlobalAverageCacheKey, async ct =>
        {
            var approved = dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved);
            return await approved.AnyAsync(ct)
                ? await approved.AverageAsync(r => (double)r.OverallRating, ct)
                : CompanyReviewScoring.NeutralAverage;
        }, GlobalAverageCacheOptions, cancellationToken: cancellationToken);

    /// <summary>The aggregate a public page shows. Cached a minute per company; approve/reject
    /// evict it, so an admin's decision shows on the next request from this instance.</summary>
    public ValueTask<CompanyReviewSummaryResponse> GetSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(SummaryCacheKey(companyId), async ct =>
        {
            var overalls = await dbContext.CompanyReviews
                .Where(r => r.CompanyId == companyId && r.Status == ReviewModerationStatus.Approved)
                .Select(r => new
                {
                    r.OverallRating, r.ManagementRating, r.WorkEnvironmentRating,
                    r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating
                })
                .ToListAsync(ct);

            var n = overalls.Count;
            var opts = options.Value;
            var distribution = new int[CompanyReview.MaxRating];
            foreach (var row in overalls)
            {
                distribution[row.OverallRating - CompanyReview.MinRating]++;
            }

            var globalAverage = n >= opts.MinimumReviewsForScore ? await GetGlobalAverageAsync(ct) : CompanyReviewScoring.NeutralAverage;

            return new CompanyReviewSummaryResponse(
                n,
                CompanyReviewScoring.BayesianScore(n, overalls.Sum(r => r.OverallRating), globalAverage, opts.PriorWeight, opts.MinimumReviewsForScore),
                opts.MinimumReviewsForScore,
                opts.PriorWeight,
                CompanyReviewScoring.CategoryAverage(n, overalls.Sum(r => r.OverallRating)),
                CompanyReviewScoring.CategoryAverage(n, overalls.Sum(r => r.ManagementRating)),
                CompanyReviewScoring.CategoryAverage(n, overalls.Sum(r => r.WorkEnvironmentRating)),
                CompanyReviewScoring.CategoryAverage(n, overalls.Sum(r => r.SalaryAndBenefitsRating)),
                CompanyReviewScoring.CategoryAverage(n, overalls.Sum(r => r.CareerAndDevelopmentRating)),
                distribution);
        }, SummaryCacheOptions, cancellationToken: cancellationToken);

    public async Task EvictSummaryAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(SummaryCacheKey(companyId), cancellationToken);
        await cache.RemoveAsync(GlobalAverageCacheKey, cancellationToken);
    }

    public async Task<int> CountUserReviewsAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.CompanyReviews.CountAsync(r => r.UserId == userId, cancellationToken);

    public async Task<ReviewQuotaResponse> GetQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var overrideValue = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ReviewQuotaOverride)
            .FirstOrDefaultAsync(cancellationToken);
        var used = await CountUserReviewsAsync(userId, cancellationToken);
        return new ReviewQuotaResponse(used, overrideValue ?? options.Value.MaxReviewsPerUser);
    }

    // Projections happen into anonymous rows and are mapped to the response records in memory:
    // EF Core translates a constructor projection, but nothing can be ordered or filtered on the
    // result afterwards, so callers order the CompanyReview query first and hand it in.

    public async Task<List<MyCompanyReviewResponse>> ProjectMineAsync(IQueryable<CompanyReview> reviews, CancellationToken cancellationToken)
    {
        var rows = await reviews
            .Join(dbContext.Companies, r => r.CompanyId, c => c.Id, (r, c) => new { Review = r, c.Slug, c.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new MyCompanyReviewResponse(
            x.Review.Id, x.Review.CompanyId, x.Slug ?? string.Empty, x.Name, x.Review.EmploymentStatus,
            x.Review.Title, x.Review.Pros, x.Review.Cons, x.Review.OverallRating, x.Review.ManagementRating,
            x.Review.WorkEnvironmentRating, x.Review.SalaryAndBenefitsRating, x.Review.CareerAndDevelopmentRating,
            x.Review.Status, x.Review.RejectionReason, x.Review.SubmittedAt, x.Review.ModeratedAt)).ToList();
    }

    public async Task<List<CompanyReviewPublicResponse>> ProjectPublicAsync(IQueryable<CompanyReview> approvedReviews,
        CancellationToken cancellationToken)
    {
        var rows = await approvedReviews
            .Select(r => new
            {
                r.Id, r.Title, r.Pros, r.Cons, r.EmploymentStatus, r.OverallRating, r.ManagementRating,
                r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating, r.SubmittedAt,
                HelpfulCount = dbContext.CompanyReviewHelpfulMarks.Count(m => m.ReviewId == r.Id)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CompanyReviewPublicResponse(
            r.Id, r.Title, r.Pros, r.Cons, r.EmploymentStatus, r.OverallRating, r.ManagementRating,
            r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating,
            // Month precision on purpose — see CompanyReviewPublicResponse.
            r.SubmittedAt.ToString("yyyy-MM"), r.HelpfulCount)).ToList();
    }

    public IOrderedQueryable<CompanyReview> OrderForPublic(IQueryable<CompanyReview> approvedReviews, PublicReviewSort sort) =>
        sort == PublicReviewSort.MostHelpful
            ? approvedReviews
                .OrderByDescending(r => dbContext.CompanyReviewHelpfulMarks.Count(m => m.ReviewId == r.Id))
                .ThenByDescending(r => r.SubmittedAt)
            : approvedReviews.OrderByDescending(r => r.SubmittedAt);

    public static ReviewContent ToContent(CreateCompanyReviewRequest r) => new(r.EmploymentStatus, r.Title, r.Pros, r.Cons,
        r.OverallRating, r.ManagementRating, r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating);

    public static ReviewContent ToContent(UpdateCompanyReviewRequest r) => new(r.EmploymentStatus, r.Title, r.Pros, r.Cons,
        r.OverallRating, r.ManagementRating, r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating);
}
