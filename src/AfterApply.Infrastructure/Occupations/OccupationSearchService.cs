using AfterApply.Application.Occupations;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.Occupations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Occupations;

/// <summary>
/// The catalogue typeahead. Both names are searched whatever the reader's language — a Turkish
/// screen typing "backend" should find "Yazılım geliştiricileri" through its English name — and
/// the web shows the language it is in. Same machinery as <c>CompanySearchService</c>: ILIKE for
/// short prefixes, the pg_trgm <c>%</c> operator for typos, ranked by similarity. Cached longer
/// than company search because the catalogue only changes by migration.
/// </summary>
internal sealed class OccupationSearchService(AppDbContext dbContext, IOptions<OccupationSearchOptions> options, HybridCache cache)
    : IOccupationSearchService
{
    private static readonly HybridCacheEntryOptions SearchCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public Task<IReadOnlyList<OccupationSearchResultResponse>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < options.Value.MinQueryLength)
        {
            return Task.FromResult<IReadOnlyList<OccupationSearchResultResponse>>([]);
        }

        var normalizedQuery = OccupationName.Normalize(trimmed);

        return cache.GetOrCreateAsync(
            $"occupation-search:{normalizedQuery}",
            normalizedQuery,
            async (nq, ct) =>
            {
                var contains = LikePattern.Contains(nq);
                var prefix = LikePattern.StartsWith(nq);

                // A name that starts with what was typed outranks a mid-word hit, which outranks a
                // fuzzy one; ties settle on the higher similarity of the two names.
                return (IReadOnlyList<OccupationSearchResultResponse>)await dbContext.Occupations
                    .Where(o => o.IsActive)
                    .Where(o => EF.Functions.ILike(o.NormalizedNameTr, contains, LikePattern.EscapeCharacter)
                        || EF.Functions.ILike(o.NormalizedNameEn, contains, LikePattern.EscapeCharacter)
                        || EF.Functions.TrigramsAreSimilar(o.NormalizedNameTr, nq)
                        || EF.Functions.TrigramsAreSimilar(o.NormalizedNameEn, nq))
                    .OrderByDescending(o => EF.Functions.ILike(o.NormalizedNameTr, prefix, LikePattern.EscapeCharacter) || EF.Functions.ILike(o.NormalizedNameEn, prefix, LikePattern.EscapeCharacter))
                    .ThenByDescending(o => EF.Functions.ILike(o.NormalizedNameTr, contains, LikePattern.EscapeCharacter) || EF.Functions.ILike(o.NormalizedNameEn, contains, LikePattern.EscapeCharacter))
                    .ThenByDescending(o => Math.Max(
                        EF.Functions.TrigramsSimilarity(o.NormalizedNameTr, nq),
                        EF.Functions.TrigramsSimilarity(o.NormalizedNameEn, nq)))
                    .ThenBy(o => o.NameEn)
                    .Take(options.Value.MaxResults)
                    .Select(o => new OccupationSearchResultResponse(o.Id, o.Code, o.NameTr, o.NameEn))
                    .ToListAsync(ct);
            },
            SearchCacheOptions,
            cancellationToken: cancellationToken).AsTask();
    }
}
