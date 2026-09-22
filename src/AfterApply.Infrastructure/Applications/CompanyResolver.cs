using AfterApply.Application.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AfterApply.Infrastructure.Applications;

internal sealed class CompanyResolver(AppDbContext dbContext, HybridCache cache, CompanySlugAllocator slugAllocator) : ICompanyResolver
{
    private static readonly HybridCacheEntryOptions LookupCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken,
        CompanyProfileLinks? profileLinks = null)
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
            if (profileLinks?.HasAny == true)
            {
                await BackfillProfileLinksAsync(existingId.Value, profileLinks, cancellationToken);
            }

            return existingId.Value;
        }

        var company = await CreateWithSlugAsync(companyName, profileLinks, cancellationToken);

        // Company.Create takes the two column-backed links, but a CompanyProfileLink row needs the
        // company's id, which only exists once the insert above has run.
        if (profileLinks?.HasAts == true)
        {
            await BackfillProfileLinksAsync(company.Id, profileLinks, cancellationToken);
        }

        // The lookup above just cached a "not found" (null) result for this key — without
        // overwriting it here, every other row in the same import batch (or any request within
        // the TTL) would see that stale null, skip the now-successful DB lookup, and attempt to
        // insert another company with the same NormalizedName, violating the unique index.
        await cache.SetAsync(cacheKey, (Guid?)company.Id, LookupCacheOptions, cancellationToken: cancellationToken);

        return company.Id;
    }

    // The slug is the public page's URL, so it has to be unique; the allocator picks the first
    // free suffix from what the table holds now, and the one race that survives (two requests
    // creating the same name at once) is caught on the index and retried once with the next one.
    //
    // The name itself can lose the same race: this instance cached "no such company" and another
    // instance created it in the meantime (the backplane closes that window to milliseconds, and
    // while Redis is down it is the whole cache TTL). Then the NormalizedName index refuses the
    // insert, and the right answer is the row that won, not a 500 — so it is looked up and
    // returned, and the cache learns the id.
    private async Task<Company> CreateWithSlugAsync(string companyName, CompanyProfileLinks? profileLinks,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var slug = await slugAllocator.AllocateAsync(companyName, cancellationToken);
            var company = Company.Create(companyName, DateTimeOffset.UtcNow,
                linkedInUrl: profileLinks?.LinkedInUrl, kariyerNetUrl: profileLinks?.KariyerNetUrl, slug: slug);
            dbContext.Companies.Add(company);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return company;
            }
            catch (DbUpdateException ex) when (attempt == 0 && CompanySlugAllocator.IsSlugCollision(ex))
            {
                dbContext.Entry(company).State = EntityState.Detached;
            }
            catch (DbUpdateException ex) when (IsNameCollision(ex))
            {
                dbContext.Entry(company).State = EntityState.Detached;
                return await dbContext.Companies.SingleAsync(c => c.NormalizedName == company.NormalizedName, cancellationToken);
            }
        }
    }

    /// <summary>The NormalizedName index saying "taken": another request created this company
    /// between our cached miss and our insert.</summary>
    internal static bool IsNameCollision(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("NormalizedName", StringComparison.Ordinal) == true;

    // A near-duplicate/exact-name match may predate the extension ever capturing a profile URL —
    // this is the only path (besides Company.Create itself) that ever writes them, so it's
    // deliberately a narrow, separate write rather than folded into the cached lookup above.
    private async Task BackfillProfileLinksAsync(Guid companyId, CompanyProfileLinks profileLinks, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .Include(c => c.ProfileLinks)
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        company.SetProfileLinksIfMissing(profileLinks.LinkedInUrl, profileLinks.KariyerNetUrl, now);

        if (profileLinks.HasAts
            && company.AddProfileLinkIfMissing(profileLinks.AtsPlatform!.Value, profileLinks.AtsUrl, now) is { } link)
        {
            // Explicitly, not just via the navigation: see Company.AddProfileLinkIfMissing for why
            // a child with a constructor-assigned key would otherwise be saved as an UPDATE.
            dbContext.CompanyProfileLinks.Add(link);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsProfileLinkCollision(ex))
        {
            // Two captures of the same company landing at once: the unique (CompanyId, Platform)
            // index refused the second one. The link is already there, which is the outcome we
            // wanted — this is a backfill, not something the caller is waiting on a result from.
            dbContext.ChangeTracker.Clear();
        }
    }

    private static bool IsProfileLinkCollision(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("CompanyProfileLinks", StringComparison.Ordinal) == true;

    private static string LookupCacheKey(string normalizedName) => $"company:normalized:{normalizedName}";
}
