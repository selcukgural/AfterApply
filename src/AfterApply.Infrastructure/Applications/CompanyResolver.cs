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

    public async Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken)
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
            return existingId.Value;
        }

        var company = Company.Create(companyName, DateTimeOffset.UtcNow);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The lookup above just cached a "not found" (null) result for this key — without
        // overwriting it here, every other row in the same import batch (or any request within
        // the TTL) would see that stale null, skip the now-successful DB lookup, and attempt to
        // insert another company with the same NormalizedName, violating the unique index.
        await cache.SetAsync(cacheKey, (Guid?)company.Id, LookupCacheOptions, cancellationToken: cancellationToken);

        return company.Id;
    }

    private static string LookupCacheKey(string normalizedName) => $"company:normalized:{normalizedName}";
}
