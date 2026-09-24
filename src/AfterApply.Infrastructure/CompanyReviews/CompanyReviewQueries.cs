using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>
/// The pieces every review service needs: the site-wide average behind the Bayesian score, a
/// company's aggregate, and the projection of a review to its author-facing and public shapes.
/// Scoped, like the services that use it, so it shares their DbContext.
/// </summary>
internal sealed class CompanyReviewQueries(
    AppDbContext dbContext,
    HybridCache cache,
    ICompanyCacheInvalidator invalidator,
    IOptions<CompanyReviewOptions> options,
    ContributionProofQueries proof)
{
    private const int TopStatements = 3;

    /// <summary>The ten optional categories, in the order the form and the summary list them.</summary>
    private static readonly ReviewCategory[] OptionalCategories =
        Enum.GetValues<ReviewCategory>().Where(c => c != ReviewCategory.Overall).ToArray();

    private static readonly HybridCacheEntryOptions GlobalAverageCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    // Ten minutes, up from one: with the backplane every write that changes the public aggregate
    // evicts it on every instance, so the TTL is no longer what bounds staleness — only a safety
    // net if Redis were down, and a cap on how long a hot company sits in memory untouched.
    private static readonly HybridCacheEntryOptions SummaryCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    /// <summary>Mean Overall rating over every approved review on the site — the prior the score
    /// pulls toward. Cached for five minutes: it moves by a rounding error per review, and every
    /// public page reads it. Falls back to the scale's midpoint while nothing is approved yet.</summary>
    public ValueTask<double> GetGlobalAverageAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(CacheKeys.Company.ReviewGlobalAverage, async ct =>
        {
            var approved = dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved);
            return await approved.AnyAsync(ct)
                ? await approved.AverageAsync(r => (double)r.OverallRating, ct)
                : CompanyReviewScoring.NeutralAverage;
        }, GlobalAverageCacheOptions, cancellationToken: cancellationToken);

    /// <summary>The aggregate a public page shows. Cached per company under the company's tag;
    /// every write that changes what is public (a structured save, an approval, a rejection, a
    /// helpful mark) drops the tag through <see cref="EvictSummaryAsync"/>.</summary>
    public ValueTask<CompanyReviewSummaryResponse> GetSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(CacheKeys.Company.ReviewSummary(companyId), async ct =>
        {
            var approved = dbContext.CompanyReviews
                .Where(r => r.CompanyId == companyId && r.Status == ReviewModerationStatus.Approved);

            var rows = await approved
                .Select(r => new { r.OverallRating, r.Format, r.ManagementRating, r.WorkEnvironmentRating, r.CareerAndDevelopmentRating })
                .ToListAsync(ct);

            var n = rows.Count;
            var opts = options.Value;
            var distribution = new int[CompanyReview.MaxRating];
            foreach (var row in rows)
            {
                distribution[row.OverallRating - CompanyReview.MinRating]++;
            }

            var globalAverage = n >= opts.MinimumReviewsForScore ? await GetGlobalAverageAsync(ct) : CompanyReviewScoring.NeutralAverage;

            // Per-category: the structured child rows, plus the legacy rows' fixed columns folded
            // into the three categories they map onto. A legacy row that was later converted has
            // Format == Structured and its own child rows, so its stale columns are skipped.
            var approvedIds = approved.Select(r => r.Id);
            var grouped = await dbContext.CompanyReviewCategoryRatings
                .Where(c => approvedIds.Contains(c.ReviewId))
                .GroupBy(c => c.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Sum = g.Sum(c => c.Rating) })
                .ToListAsync(ct);

            var counts = OptionalCategories.ToDictionary(c => c, _ => 0);
            var sums = OptionalCategories.ToDictionary(c => c, _ => 0);
            foreach (var g in grouped)
            {
                counts[g.Category] += g.Count;
                sums[g.Category] += g.Sum;
            }

            foreach (var row in rows.Where(r => r.Format == ReviewFormat.Legacy))
            {
                Fold(ReviewCategory.Management, row.ManagementRating);
                Fold(ReviewCategory.WorkEnvironment, row.WorkEnvironmentRating);
                Fold(ReviewCategory.CareerGrowth, row.CareerAndDevelopmentRating);
            }

            var categories = OptionalCategories
                .Select(c => new ReviewCategoryAverageResponse(c, counts[c],
                    CompanyReviewScoring.ThresholdedAverage(counts[c], sums[c], opts.MinimumReviewsForScore)))
                .ToList();

            // "Most picked" lists: below the threshold they would be one person's opinion with a
            // number next to it.
            var topLiked = new List<ReviewStatementCountResponse>();
            var topImprovable = new List<ReviewStatementCountResponse>();
            if (n >= opts.MinimumReviewsForScore)
            {
                var picks = await dbContext.CompanyReviewStatementPicks
                    .Where(p => approvedIds.Contains(p.ReviewId))
                    .GroupBy(p => new { p.Kind, p.StatementKey })
                    .Select(g => new { g.Key.Kind, g.Key.StatementKey, Count = g.Count() })
                    .ToListAsync(ct);

                topLiked = Top(ReviewStatementKind.Liked);
                topImprovable = Top(ReviewStatementKind.Improve);

                List<ReviewStatementCountResponse> Top(ReviewStatementKind kind) => picks
                    .Where(p => p.Kind == kind)
                    .OrderByDescending(p => p.Count).ThenBy(p => p.StatementKey, StringComparer.Ordinal)
                    .Take(TopStatements)
                    .Select(p => new ReviewStatementCountResponse(p.StatementKey, p.Count))
                    .ToList();
            }

            return new CompanyReviewSummaryResponse(
                n,
                CompanyReviewScoring.BayesianScore(n, rows.Sum(r => r.OverallRating), globalAverage, opts.PriorWeight, opts.MinimumReviewsForScore),
                opts.MinimumReviewsForScore,
                opts.PriorWeight,
                CompanyReviewScoring.CategoryAverage(n, rows.Sum(r => r.OverallRating)),
                categories,
                distribution,
                topLiked,
                topImprovable);

            void Fold(ReviewCategory category, int? rating)
            {
                if (rating is { } value)
                {
                    counts[category]++;
                    sums[category] += value;
                }
            }
        }, SummaryCacheOptions, tags: [CacheKeys.Company.Tag(companyId)], cancellationToken: cancellationToken);

    /// <summary>Everything cached for the company, not just the summary — the page, the review
    /// list pages, the directory. See <see cref="ICompanyCacheInvalidator"/>.</summary>
    public ValueTask EvictSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        invalidator.InvalidateCompanyAsync(companyId, cancellationToken);

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
    // result afterwards, so callers order the CompanyReview query first and hand it in. The child
    // rows (category ratings, statement picks) come in two further queries keyed by the page's
    // review ids — a page is at most ten rows, and no navigation collection means no Include.

    public async Task<List<MyCompanyReviewResponse>> ProjectMineAsync(IQueryable<CompanyReview> reviews, CancellationToken cancellationToken)
    {
        var rows = await reviews
            .Join(dbContext.Companies, r => r.CompanyId, c => c.Id, (r, c) => new { Review = r, c.Slug, c.Name })
            .ToListAsync(cancellationToken);

        var children = await LoadChildrenAsync(rows.Select(x => x.Review.Id), cancellationToken);

        return rows.Select(x =>
        {
            var r = x.Review;
            var c = children.For(r);
            return new MyCompanyReviewResponse(
                r.Id, r.CompanyId, x.Slug ?? string.Empty, x.Name, r.Format, r.EmploymentStatus, r.OverallRating,
                c.CategoryRatings, c.LegacySalaryAndBenefits, c.Liked, c.Improvable,
                r.Title, r.Pros, r.Cons,
                r.Status, r.RejectionReason, r.SubmittedAt, r.ModeratedAt);
        }).ToList();
    }

    public async Task<List<CompanyReviewPublicResponse>> ProjectPublicAsync(IQueryable<CompanyReview> approvedReviews,
        CancellationToken cancellationToken)
    {
        // Title/Pros/Cons are not selected here at all — the public record has no place for them.
        var rows = await approvedReviews
            .Select(r => new
            {
                r.Id, r.Format, r.EmploymentStatus, r.OverallRating, r.ManagementRating, r.WorkEnvironmentRating,
                r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating, r.SubmittedAt,
                HelpfulCount = dbContext.CompanyReviewHelpfulMarks.Count(m => m.ReviewId == r.Id)
            })
            .ToListAsync(cancellationToken);

        var children = await LoadChildrenAsync(rows.Select(r => r.Id), cancellationToken);
        var backed = await proof.BackedReviewIdsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);

        return rows.Select(r =>
        {
            var c = children.For(r.Id, r.Format, r.ManagementRating, r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating);
            return new CompanyReviewPublicResponse(
                r.Id, r.Format, r.EmploymentStatus, r.OverallRating,
                c.CategoryRatings, c.LegacySalaryAndBenefits, c.Liked, c.Improvable,
                // Month precision on purpose — see CompanyReviewPublicResponse.
                r.SubmittedAt.ToString("yyyy-MM"), r.HelpfulCount, backed.Contains(r.Id));
        }).ToList();
    }

    public IOrderedQueryable<CompanyReview> OrderForPublic(IQueryable<CompanyReview> approvedReviews, PublicReviewSort sort) =>
        sort == PublicReviewSort.MostHelpful
            ? approvedReviews
                .OrderByDescending(r => dbContext.CompanyReviewHelpfulMarks.Count(m => m.ReviewId == r.Id))
                .ThenByDescending(r => r.SubmittedAt)
            : approvedReviews.OrderByDescending(r => r.SubmittedAt);

    /// <summary>The child rows of a set of reviews, grouped so a projection can be built in memory.</summary>
    public async Task<ReviewChildren> LoadChildrenAsync(IEnumerable<Guid> reviewIds, CancellationToken cancellationToken)
    {
        var ids = reviewIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new ReviewChildren(
                Enumerable.Empty<ReviewCategoryRatingDto>().ToLookup(_ => Guid.Empty),
                Enumerable.Empty<(string, ReviewStatementKind)>().ToLookup(_ => Guid.Empty));
        }

        var ratings = await dbContext.CompanyReviewCategoryRatings
            .Where(c => ids.Contains(c.ReviewId))
            .Select(c => new { c.ReviewId, c.Category, c.Rating })
            .ToListAsync(cancellationToken);

        // Ids are version-7 GUIDs, so ordering by Id is the order the author picked them in.
        var picks = await dbContext.CompanyReviewStatementPicks
            .Where(p => ids.Contains(p.ReviewId))
            .OrderBy(p => p.Id)
            .Select(p => new { p.ReviewId, p.StatementKey, p.Kind })
            .ToListAsync(cancellationToken);

        return new ReviewChildren(
            ratings.ToLookup(x => x.ReviewId, x => new ReviewCategoryRatingDto(x.Category, x.Rating)),
            picks.ToLookup(x => x.ReviewId, x => (x.StatementKey, x.Kind)));
    }

    public static StructuredReviewContent ToContent(CreateCompanyReviewRequest r) =>
        ToContent(r.EmploymentStatus, r.OverallRating, r.CategoryRatings, r.LikedStatements, r.ImprovableStatements);

    public static StructuredReviewContent ToContent(UpdateCompanyReviewRequest r) =>
        ToContent(r.EmploymentStatus, r.OverallRating, r.CategoryRatings, r.LikedStatements, r.ImprovableStatements);

    private static StructuredReviewContent ToContent(EmploymentStatus status, int overall,
        IReadOnlyList<ReviewCategoryRatingDto>? ratings, IReadOnlyList<string>? liked, IReadOnlyList<string>? improvable) =>
        new(status, overall,
            (ratings ?? []).Select(x => new CategoryRating(x.Category, x.Rating)).ToList(),
            liked ?? [],
            improvable ?? []);

    /// <summary>What a review's child rows and, for a legacy row, its fixed columns amount to.</summary>
    public sealed record ReviewChildren(
        ILookup<Guid, ReviewCategoryRatingDto> Ratings,
        ILookup<Guid, (string Key, ReviewStatementKind Kind)> Picks)
    {
        public Projection For(CompanyReview r) =>
            For(r.Id, r.Format, r.ManagementRating, r.WorkEnvironmentRating, r.SalaryAndBenefitsRating, r.CareerAndDevelopmentRating);

        /// <summary>A legacy row shows the three fixed ratings that map onto a current category
        /// and keeps the fourth apart; a structured row shows its child rows and nothing else.</summary>
        public Projection For(Guid id, ReviewFormat format, int? management, int? workEnvironment, int? salaryAndBenefits, int? career)
        {
            if (format == ReviewFormat.Legacy)
            {
                var legacy = new List<ReviewCategoryRatingDto>(3);
                Add(ReviewCategory.WorkEnvironment, workEnvironment);
                Add(ReviewCategory.Management, management);
                Add(ReviewCategory.CareerGrowth, career);
                return new Projection(legacy, salaryAndBenefits, [], []);

                void Add(ReviewCategory category, int? rating)
                {
                    if (rating is { } value)
                    {
                        legacy.Add(new ReviewCategoryRatingDto(category, value));
                    }
                }
            }

            var picks = Picks[id].ToList();
            return new Projection(
                Ratings[id].OrderBy(x => x.Category).ToList(),
                null,
                picks.Where(p => p.Kind == ReviewStatementKind.Liked).Select(p => p.Key).ToList(),
                picks.Where(p => p.Kind == ReviewStatementKind.Improve).Select(p => p.Key).ToList());
        }
    }

    public sealed record Projection(
        IReadOnlyList<ReviewCategoryRatingDto> CategoryRatings,
        int? LegacySalaryAndBenefits,
        IReadOnlyList<string> Liked,
        IReadOnlyList<string> Improvable);
}
