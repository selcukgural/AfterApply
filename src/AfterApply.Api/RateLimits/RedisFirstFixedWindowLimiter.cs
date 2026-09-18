using System.Diagnostics;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace AfterApply.Api.RateLimits;

/// <summary>
/// One partition's limiter: a fixed window counted in Redis, so the limit is one limit across
/// every API instance, with the in-memory fixed window as the fallback while Redis is unreachable.
/// Before 2026-09-18 every window was in-memory and therefore per instance — with four instances
/// "five CV scans per two hours" was twenty — and a limit that only bounds the instance a request
/// happens to land on is not a limit.
///
/// The fallback is what keeps a Redis outage from turning every rate-limited endpoint into a 500:
/// the Redis limiter lets <see cref="RedisConnectionException"/> and
/// <see cref="RedisTimeoutException"/> escape <c>AcquireAsync</c>, and the middleware does not
/// catch them. Falling back to the local window means an outage degrades to the pre-2026-09-18
/// per-instance behaviour, never to an open door and never to an outage of our own. The local
/// limiter's counters are independent of Redis's — a caller whose window was half spent in Redis
/// starts a fresh local one — which is the accepted cost of the fallback.
/// </summary>
public sealed class RedisFirstFixedWindowLimiter : RateLimiter
{
    private readonly RateLimiter _redis;
    private readonly RateLimiter _local;
    private readonly Func<bool> _redisIsConnected;
    private readonly Action<Exception> _onFallback;

    private long _idleSince = Stopwatch.GetTimestamp();
    private int _active;

    public RedisFirstFixedWindowLimiter(RateLimiter redis, RateLimiter local, Func<bool> redisIsConnected, Action<Exception> onFallback)
    {
        _redis = redis;
        _local = local;
        _redisIsConnected = redisIsConnected;
        _onFallback = onFallback;
    }

    /// <summary>
    /// Non-null while idle, or PartitionedRateLimiter never evicts the partition: its heartbeat
    /// drops partitions whose limiter reports an idle duration past its threshold, and the
    /// partitions here are keyed by IP, so a limiter that never reports idle is a dictionary that
    /// grows with every address that ever called.
    /// </summary>
    public override TimeSpan? IdleDuration =>
        Volatile.Read(ref _active) > 0 ? null : Stopwatch.GetElapsedTime(Volatile.Read(ref _idleSince));

    public override RateLimiterStatistics? GetStatistics() => _local.GetStatistics();

    /// <summary>
    /// Always "not acquired", and deliberately so. RateLimitingMiddleware tries the synchronous
    /// AttemptAcquire first and only falls through to AcquireAsync when that is refused; answering
    /// here from the local window would let the first PermitLimit requests through without Redis
    /// ever seeing them — measured on 2026-09-18: six failed logins, Redis counted two. Refusing
    /// synchronously costs nothing (the middleware's next call is the async one, on this same
    /// request) and is what the Redis limiter itself does for the same reason.
    /// </summary>
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => DeferredToAsync.Instance;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _active);
        try
        {
            // A multiplexer that knows it is disconnected fails fast, but not for free: skip the
            // round trip and its exception when the answer is already known.
            if (!_redisIsConnected())
            {
                return await _local.AcquireAsync(permitCount, cancellationToken);
            }

            try
            {
                return await _redis.AcquireAsync(permitCount, cancellationToken);
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
            {
                _onFallback(ex);
                return await _local.AcquireAsync(permitCount, cancellationToken);
            }
        }
        finally
        {
            Volatile.Write(ref _idleSince, Stopwatch.GetTimestamp());
            Interlocked.Decrement(ref _active);
        }
    }

    private sealed class DeferredToAsync : RateLimitLease
    {
        public static readonly DeferredToAsync Instance = new();

        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _redis.Dispose();
            _local.Dispose();
        }

        base.Dispose(disposing);
    }
}
