using AfterApply.Application.SiteStats;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.SiteStats;

/// <summary>Three counts, cached together for <see cref="SiteStatsOptions.CacheSeconds"/> — the
/// public landing page asks for them on every render, and the number changing an hour late is
/// not a bug anyone will notice.</summary>
internal sealed class SiteStatsService(AppDbContext dbContext, HybridCache cache, IOptions<SiteStatsOptions> options)
    : ISiteStatsService
{
    private const string CacheKey = "site-stats";

    public async Task<SiteStatsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(options.Value.CacheSeconds),
            LocalCacheExpiration = TimeSpan.FromSeconds(options.Value.CacheSeconds)
        };

        return await cache.GetOrCreateAsync(CacheKey, async ct =>
        {
            var minimum = options.Value.MinimumCount;
            var cvScans = await dbContext.CvScanResults.CountAsync(ct);
            var benchmarkAnswers = await dbContext.BenchmarkSubmissions.CountAsync(ct);
            var publishedReviews = await dbContext.CompanyReviews
                .CountAsync(r => r.Status == ReviewModerationStatus.Approved, ct);

            return new SiteStatsResponse(
                SiteStatsRules.Visible(cvScans, minimum),
                SiteStatsRules.Visible(benchmarkAnswers, minimum),
                SiteStatsRules.Visible(publishedReviews, minimum));
        }, entryOptions, cancellationToken: cancellationToken);
    }
}
