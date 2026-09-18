using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
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
    IOptions<CandidateExperienceOptions> experienceOptions) : ICompanyDirectoryService
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
        var salariesOn = salaryOptions.Value.Enabled;
        var experiencesOn = experienceOptions.Value.Enabled;

        // Only companies somebody has contributed to — a published review, a salary entry or a
        // candidate experience (2026-09-18; until then reviews alone counted, so a company whose
        // first contribution was a salary never reached the directory). The three tables become
        // one (CompanyId, At) union; a feature that is off contributes nothing to it. Correlated
        // subqueries rather than GroupBy+Join, which EF Core cannot translate here.
        // Anonymous type on purpose: Concat needs the three projections to share one CLR shape,
        // and same-named members of the same types unify to one anonymous type.
        var contributions = approved.Select(r => new { r.CompanyId, At = r.SubmittedAt });
        if (salariesOn)
        {
            contributions = contributions.Concat(dbContext.CompanySalaryEntries.Select(s => new { s.CompanyId, At = s.SubmittedAt }));
        }

        if (experiencesOn)
        {
            contributions = contributions.Concat(dbContext.CandidateExperiences.Select(e => new { e.CompanyId, At = e.SubmittedAt }));
        }

        var companies = dbContext.Companies.Where(c => c.Slug != null && contributions.Any(x => x.CompanyId == c.Id));
        var q = query.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var pattern = $"%{TurkishTextNormalizer.FoldCase(q).ToUpperInvariant()}%";
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern));
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
        // A count is not sensitive, and it is what lets the public page label its "Salaries" tab
        // before the reader signs in. One indexed COUNT, not cached with the review summary.
        var salaryCount = salaryOptions.Value.Enabled
            ? await dbContext.CompanySalaryEntries.CountAsync(s => s.CompanyId == company.Id, cancellationToken)
            : 0;
        // Same shape for the third tab: a count, off → zero.
        var experienceCount = experienceOptions.Value.Enabled
            ? await dbContext.CandidateExperiences.CountAsync(e => e.CompanyId == company.Id, cancellationToken)
            : 0;
        return new CompanyPublicResponse(company.Id, slug, company.Name, company.Website, summary, salaryCount, experienceCount);
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
        }, ReviewedSlugsCacheOptions, cancellationToken: cancellationToken);
}
