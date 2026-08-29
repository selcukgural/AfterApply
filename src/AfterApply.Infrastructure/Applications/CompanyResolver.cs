using AfterApply.Application.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AfterApply.Infrastructure.Applications;

internal sealed class CompanyResolver(AppDbContext dbContext, HybridCache cache) : ICompanyResolver
{
    private static readonly HybridCacheEntryOptions LookupCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken, string? linkedInUrl = null)
    {
        var normalizedName = CompanyNameNormalizer.Normalize(companyName);
        var cacheKey = LookupCacheKey(normalizedName);

        // Cache-aside, no explicit invalidation: companies are effectively append-only through
        // this path, so a miss just re-resolves and re-populates the cache.
        var existingId = await cache.GetOrCreateAsync<Guid?>(
            cacheKey,
            async ct => await dbContext.Companies
                .Where(c => c.NormalizedName == normalizedName)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct),
            LookupCacheOptions,
            cancellationToken: cancellationToken);

        if (existingId is not null)
        {
            if (linkedInUrl is not null)
            {
                await BackfillLinkedInUrlAsync(existingId.Value, linkedInUrl, cancellationToken);
            }

            return existingId.Value;
        }

        var company = Company.Create(companyName, DateTimeOffset.UtcNow, linkedInUrl: linkedInUrl);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The lookup above just cached a "not found" (null) result for this key — without
        // overwriting it here, every other row in the same import batch (or any request within
        // the TTL) would see that stale null, skip the now-successful DB lookup, and attempt to
        // insert another company with the same NormalizedName, violating the unique index.
        await cache.SetAsync(cacheKey, (Guid?)company.Id, LookupCacheOptions, cancellationToken: cancellationToken);

        return company.Id;
    }

    // A near-duplicate/exact-name match may predate the extension ever capturing a LinkedIn URL —
    // this is the only path (besides Company.Create itself) that ever writes LinkedInUrl, so it's
    // deliberately a narrow, separate write rather than folded into the cached lookup above.
    private async Task BackfillLinkedInUrlAsync(Guid companyId, string linkedInUrl, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null || company.LinkedInUrl is not null)
        {
            return;
        }

        company.SetLinkedInUrlIfMissing(linkedInUrl, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string LookupCacheKey(string normalizedName) => $"company:normalized:{normalizedName}";
}
