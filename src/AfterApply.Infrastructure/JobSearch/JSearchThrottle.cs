using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// In-process gate on how many calls a second leave for the provider. A token bucket from
/// <c>System.Threading.RateLimiting</c> rather than a hand-rolled queue: it is already a
/// thread-safe FIFO that parks concurrent callers and releases them as tokens refill. Off
/// (<c>RequestsPerSecond</c> = 0) it costs a null check — the BASIC plan has no per-second rule.
/// Singleton, because the bucket is only a bucket if every caller shares it.
/// </summary>
public sealed class JSearchThrottle : IDisposable
{
    private const int MaxQueuedCallers = 1000;

    private readonly TokenBucketRateLimiter? _limiter;

    public JSearchThrottle(IOptions<JobSearchOptions> options)
    {
        var perSecond = options.Value.RequestsPerSecond;
        if (perSecond <= 0)
        {
            return;
        }

        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = perSecond,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = MaxQueuedCallers,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public bool IsEnabled => _limiter is not null;

    /// <summary>Returns once this caller may send. Throws <see cref="JSearchException"/>
    /// (<see cref="JSearchFailure.RateLimited"/>) only if the queue itself is full.</summary>
    public async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        if (_limiter is null)
        {
            return;
        }

        using var lease = await _limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new JSearchException(JSearchFailure.RateLimited, null, null,
                "Too many job search calls are already waiting to be sent.");
        }
    }

    public void Dispose() => _limiter?.Dispose();
}
