using AfterApply.Infrastructure.Companies;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>The anonymous side. Every review query here starts from
/// <c>Status == Approved</c> — the filter is in the query, not in a mapper that could be bypassed.
/// Salary entries and candidate experiences have no moderation state: saved is published.</summary>
internal sealed class CompanyDirectoryService(
    AppDbContext dbContext,
    CompanyReviewQueries queries,
    HybridCache cache,
    IOptions<CompanyReviewOptions> options,
    IOptions<CompanySalaryOptions> salaryOptions,
    IOptions<CandidateExperienceOptions> experienceOptions,
    CompanyVisibility visibility) : ICompanyDirectoryService
{
    // Every entry in this service sits under a tag — the company's for its own page and review
    // pages, the directory's for the lists that span companies — and every contribution write
    // drops both through ICompanyCacheInvalidator, on every instance. The TTLs only bound
    // staleness while Redis is unreachable.
    private static readonly HybridCacheEntryOptions CompanyCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    private static readonly HybridCacheEntryOptions DirectoryCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private static readonly HybridCacheEntryOptions ReviewedSlugsCacheOptions = CompanyCacheOptions;

    public async Task<PagedResult<CompanyPublicListItemResponse>> ListAsync(PublicCompanyListQuery query, CancellationToken cancellationToken)
    {
        // A search is not cached: its key space is the user's input. The plain directory pages
        // are what every visitor and every crawler read, and they are a handful of keys.
        if (string.IsNullOrWhiteSpace(query.Q))
        {
            return await cache.GetOrCreateAsync(CacheKeys.Company.DirectoryPage(query.Page),
                ct => new ValueTask<PagedResult<CompanyPublicListItemResponse>>(QueryDirectoryAsync(query, ct)),
                DirectoryCacheOptions, tags: [CacheKeys.Company.DirectoryTag], cancellationToken: cancellationToken);
        }

        return await QueryDirectoryAsync(query, cancellationToken);
    }

    private async Task<PagedResult<CompanyPublicListItemResponse>> QueryDirectoryAsync(PublicCompanyListQuery query, CancellationToken cancellationToken)
    {
        var approved = dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved);
        var salariesOn = salaryOptions.Value.Enabled;
        var experiencesOn = experienceOptions.Value.Enabled;
        var contributions = Contributions(approved);

        // Only companies somebody has contributed to (see Contributions).
        var companies = dbContext.Companies.Where(c => c.Slug != null && contributions.Any(x => x.CompanyId == c.Id));
        var q = query.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var pattern = NamePattern(q);
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern, LikePattern.EscapeCharacter));
        }

        // The two feature-gated counts are always computed in SQL and zeroed in memory when the
        // feature is off: a flag inside the projection would become a CASE WHEN for nothing.
        var joined = companies.Select(c => new
        {
            c.Id,
            Slug = c.Slug!,
            c.Name,
            Count = approved.Count(r => r.CompanyId == c.Id),
            Sum = approved.Where(r => r.CompanyId == c.Id).Sum(r => r.OverallRating),
            SalaryCount = dbContext.CompanySalaryEntries.Count(s => s.CompanyId == c.Id),
            ExperienceCount = dbContext.CandidateExperiences.Count(e => e.CompanyId == c.Id),
            LatestAt = contributions.Where(x => x.CompanyId == c.Id).Max(x => (DateTimeOffset?)x.At)
        });

        var total = await joined.CountAsync(cancellationToken);
        var pageSize = options.Value.PublicPageSize;
        var rows = await joined
            // The company that got a contribution most recently is on top; the count used to be
            // the key, which pinned the same few names to page one forever.
            .OrderByDescending(x => x.LatestAt).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var opts = options.Value;
        var globalAverage = rows.Any(r => r.Count >= opts.MinimumReviewsForScore)
            ? await queries.GetGlobalAverageAsync(cancellationToken)
            : CompanyReviewScoring.NeutralAverage;

        var items = rows.Select(r => new CompanyPublicListItemResponse(r.Id, r.Slug, r.Name, r.Count,
                CompanyReviewScoring.BayesianScore(r.Count, r.Sum, globalAverage, opts.PriorWeight, opts.MinimumReviewsForScore),
                salariesOn ? r.SalaryCount : 0,
                experiencesOn ? r.ExperienceCount : 0))
            .ToList();

        return new PagedResult<CompanyPublicListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<IReadOnlyList<KnownCompanyResponse>> SearchKnownAsync(string? q, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var trimmed = q?.Trim() ?? string.Empty;
        if (trimmed.Length < opts.KnownCompanyMinimumQueryLength)
        {
            return [];
        }

        // Not cached, like any search: the key space is the user's input.
        var contributions = Contributions(dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved));
        var pattern = NamePattern(trimmed);
        var minimum = opts.KnownCompanyMinimumApplicants;

        // The complement of the directory — a page, no contribution — narrowed to the names enough
        // different people applied to. Distinct users, never applications: one person's forty
        // applications to the same firm are still one person.
        return await dbContext.Companies
            .Where(c => c.Slug != null
                        && EF.Functions.ILike(c.NormalizedName, pattern, LikePattern.EscapeCharacter)
                        && !contributions.Any(x => x.CompanyId == c.Id)
                        && dbContext.Applications.Where(a => a.CompanyId == c.Id).Select(a => a.UserId).Distinct().Count() >= minimum)
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Take(opts.KnownCompanyResultLimit)
            .Select(c => new KnownCompanyResponse(c.Slug!, c.Name))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Every company somebody has contributed to — a published review, a salary entry or
    /// a candidate experience (2026-09-18; until then reviews alone counted, so a company whose
    /// first contribution was a salary never reached the directory) — as one (CompanyId, At)
    /// union; a feature that is off contributes nothing to it. Correlated subqueries use it rather
    /// than GroupBy+Join, which EF Core cannot translate here. One member-init shape for all
    /// three projections, because Concat needs them to match.</summary>
    private IQueryable<ContributionRow> Contributions(IQueryable<CompanyReview> approved)
    {
        var contributions = approved.Select(r => new ContributionRow { CompanyId = r.CompanyId, At = r.SubmittedAt });
        if (salaryOptions.Value.Enabled)
        {
            contributions = contributions.Concat(dbContext.CompanySalaryEntries.Select(s => new ContributionRow { CompanyId = s.CompanyId, At = s.SubmittedAt }));
        }

        if (experienceOptions.Value.Enabled)
        {
            contributions = contributions.Concat(dbContext.CandidateExperiences.Select(e => new ContributionRow { CompanyId = e.CompanyId, At = e.SubmittedAt }));
        }

        return contributions;
    }

    private static string NamePattern(string q) => LikePattern.Contains(TurkishTextNormalizer.FoldCase(q).ToUpperInvariant());

    private sealed class ContributionRow
    {
        public Guid CompanyId { get; init; }

        public DateTimeOffset At { get; init; }
    }

    public async Task<CompanyPublicResponse?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        // The slug → id step stays a query so the cache entry can carry the company's tag (a tag
        // has to be known before the entry is built); it is one indexed row.
        var companyId = await CompanyIdBySlugAsync(slug, cancellationToken);
        if (companyId is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(CacheKeys.Company.PublicPage(companyId.Value), async ct =>
        {
            var company = await dbContext.Companies
                .Where(c => c.Id == companyId)
                .Select(c => new { c.Name, c.Website })
                .FirstAsync(ct);
            // Read from a page one user's capture pointed at; public only once enough different
            // people pointed at the same page (CompanyVisibility). The page's cache is dropped when
            // a new one does (CompanyResolver.RecordProfileSubmissionsAsync).
            var website = (await visibility.ConfirmedWebsitesAsync([companyId.Value], ct)).Contains(companyId.Value)
                ? company.Website
                : null;
            var summary = await queries.GetSummaryAsync(companyId.Value, ct);
            // A count is not sensitive, and it is what lets the public page label its "Salaries"
            // tab before the reader signs in. One indexed COUNT each.
            var salaryCount = salaryOptions.Value.Enabled
                ? await dbContext.CompanySalaryEntries.CountAsync(s => s.CompanyId == companyId, ct)
                : 0;
            // Same shape for the third tab: a count, off → zero.
            var experienceCount = experienceOptions.Value.Enabled
                ? await dbContext.CandidateExperiences.CountAsync(e => e.CompanyId == companyId, ct)
                : 0;
            return new CompanyPublicResponse(companyId.Value, slug, company.Name, website, summary, salaryCount, experienceCount);
        }, CompanyCacheOptions, tags: [CacheKeys.Company.Tag(companyId.Value)], cancellationToken: cancellationToken);
    }

    // Only a listed company has a public page (see CompanyVisibility): any other slug answers like
    // one that does not exist, so the page cannot say "somebody applied here".
    private Task<Guid?> CompanyIdBySlugAsync(string slug, CancellationToken cancellationToken) =>
        visibility.Listed(dbContext.Companies)
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<CompanyReviewPublicResponse>?> ListApprovedReviewsAsync(string slug, PublicReviewListQuery query,
        CancellationToken cancellationToken)
    {
        var companyId = await CompanyIdBySlugAsync(slug, cancellationToken);
        if (companyId is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(CacheKeys.Company.ReviewList(companyId.Value, query.Page, query.Sort.ToString()), async ct =>
        {
            var approved = dbContext.CompanyReviews
                .Where(r => r.CompanyId == companyId && r.Status == ReviewModerationStatus.Approved);

            var total = await approved.CountAsync(ct);
            var pageSize = options.Value.PublicPageSize;

            var items = await queries.ProjectPublicAsync(
                queries.OrderForPublic(approved, query.Sort)
                    .ThenBy(r => r.Id)
                    .Skip((query.Page - 1) * pageSize)
                    .Take(pageSize),
                ct);

            return new PagedResult<CompanyReviewPublicResponse>(items, total, query.Page, pageSize);
        }, CompanyCacheOptions, tags: [CacheKeys.Company.Tag(companyId.Value)], cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewedCompanySlugResponse>> ListReviewedSlugsAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(CacheKeys.Company.ReviewedSlugs, async ct =>
        {
            // A structured review is published on save and never carries ModeratedAt; a legacy
            // one became public when a human approved it. Either way, "last changed" is the later.
            // A candidate experience is public from the moment it is saved, so it counts too;
            // salaries do not — they sit behind sign-in, a crawler sees nothing there.
            var approved = dbContext.CompanyReviews.Where(r => r.Status == ReviewModerationStatus.Approved);
            var stamps = approved.Select(r => new { r.CompanyId, At = r.ModeratedAt ?? r.SubmittedAt });
            if (experienceOptions.Value.Enabled)
            {
                stamps = stamps.Concat(dbContext.CandidateExperiences.Select(e => new { e.CompanyId, At = e.SubmittedAt }));
            }

            var rows = await dbContext.Companies
                .Where(c => c.Slug != null && stamps.Any(x => x.CompanyId == c.Id))
                .OrderBy(c => c.Slug)
                .Select(c => new ReviewedCompanySlugResponse(
                    c.Slug!,
                    stamps.Where(x => x.CompanyId == c.Id).Max(x => x.At)))
                .ToListAsync(ct);
            return (IReadOnlyList<ReviewedCompanySlugResponse>)rows;
        }, ReviewedSlugsCacheOptions, tags: [CacheKeys.Company.DirectoryTag], cancellationToken: cancellationToken);
}
