using Microsoft.Extensions.Caching.Hybrid;

namespace AfterApply.Infrastructure.Caching;

/// <summary>
/// The single call every write to a company's public data makes after its <c>SaveChangesAsync</c>.
/// It drops the company's tag (page, summaries, review/experience/salary lists), the cross-company
/// tag (directory pages, sitemap slugs) and the two site-wide averages the Bayesian scores are
/// anchored to. Callers do not pick which of those apply: the cost of over-invalidating is one
/// extra DB read on the next request, the cost of under-invalidating is a stale public page —
/// exactly the bug that brought Redis back (DECISIONS.md 2026-09-18).
/// </summary>
internal interface ICompanyCacheInvalidator
{
    ValueTask InvalidateCompanyAsync(Guid companyId, CancellationToken cancellationToken);
}

internal sealed class CompanyCacheInvalidator(HybridCache cache) : ICompanyCacheInvalidator
{
    public async ValueTask InvalidateCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await cache.RemoveByTagAsync(CacheKeys.Company.Tag(companyId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.Company.DirectoryTag, cancellationToken);
        await cache.RemoveAsync(CacheKeys.Company.ReviewGlobalAverage, cancellationToken);
        await cache.RemoveAsync(CacheKeys.Company.ExperienceGlobalAverage, cancellationToken);
    }
}
