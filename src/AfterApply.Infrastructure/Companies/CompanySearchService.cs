using AfterApply.Application.Companies;
using AfterApply.Application.Companies.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Companies;

internal sealed class CompanySearchService(AppDbContext dbContext, IOptions<CompanySearchOptions> options, HybridCache cache) : ICompanySearchService
{
    private static readonly HybridCacheEntryOptions SearchCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public Task<IReadOnlyList<CompanySearchResultResponse>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < options.Value.MinQueryLength)
        {
            return Task.FromResult<IReadOnlyList<CompanySearchResultResponse>>([]);
        }

        // Suffix-stripping (CompanyNameNormalizer.Normalize's full pipeline) is meaningless — and
        // can be actively wrong — on a partial, mid-typing string, so only the cheap case-fold
        // step is applied here, keeping the query in the same alphabet as the indexed column.
        var normalizedQuery = TurkishTextNormalizer.FoldCase(trimmed).ToUpperInvariant();

        // No invalidation: a newly-added company not showing up in autocomplete for up to 30s is
        // a non-issue, and this only saves repeated identical keystrokes/retries hitting Postgres.
        return cache.GetOrCreateAsync(
            $"company-search:{normalizedQuery}",
            normalizedQuery,
            async (nq, ct) =>
            {
                var pattern = $"%{nq}%";

                // ILIKE substring recall covers short prefixes, where trigram similarity() alone is a
                // weak signal (too few 3-char n-grams); TrigramsAreSimilar (the pg_trgm `%` operator)
                // adds a fuzzy net for typo'd/reordered names that don't substring-match. Both candidate
                // sets are then ranked together by actual similarity.
                return (IReadOnlyList<CompanySearchResultResponse>)await dbContext.Companies
                    .Where(c => EF.Functions.ILike(c.NormalizedName, pattern)
                        || EF.Functions.TrigramsAreSimilar(c.NormalizedName, nq))
                    .OrderByDescending(c => EF.Functions.TrigramsSimilarity(c.NormalizedName, nq))
                    .ThenBy(c => c.Name)
                    .Take(options.Value.MaxResults)
                    .Select(c => new CompanySearchResultResponse(c.Id, c.Name, c.Website))
                    .ToListAsync(ct);
            },
            SearchCacheOptions,
            cancellationToken: cancellationToken).AsTask();
    }

    public async Task<Guid?> FindHighConfidenceMatchAsync(string companyName, CancellationToken cancellationToken)
    {
        var normalizedName = CompanyNameNormalizer.Normalize(companyName);
        var threshold = options.Value.FuzzyMatchThreshold;

        return await dbContext.Companies
            .Where(c => EF.Functions.TrigramsSimilarity(c.NormalizedName, normalizedName) >= threshold)
            .OrderByDescending(c => EF.Functions.TrigramsSimilarity(c.NormalizedName, normalizedName))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
