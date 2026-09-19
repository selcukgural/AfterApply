using AfterApply.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace AfterApply.Infrastructure.Blog;

/// <summary>The single call every write to the public blog makes after its
/// <c>SaveChangesAsync</c> — the <c>ICompanyCacheInvalidator</c> shape, one tag.</summary>
internal interface IBlogCacheInvalidator
{
    ValueTask InvalidateAsync(CancellationToken cancellationToken);
}

internal sealed class BlogCacheInvalidator(HybridCache cache) : IBlogCacheInvalidator
{
    public ValueTask InvalidateAsync(CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(CacheKeys.Blog.Tag, cancellationToken);
}
