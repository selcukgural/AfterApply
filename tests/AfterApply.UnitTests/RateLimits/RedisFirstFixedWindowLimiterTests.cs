using System.Threading.RateLimiting;
using AfterApply.Api.RateLimits;
using Shouldly;
using StackExchange.Redis;

namespace AfterApply.UnitTests.RateLimits;

/// <summary>
/// The fallback contract of the limiter every rate-limit partition is built on (RateLimiting.cs):
/// Redis answers when it can, the local window answers when it cannot, and never a 500 either way.
/// The two inner limiters are stand-ins here — the Redis one is a limiter that throws the way
/// StackExchange.Redis does — because what is under test is the routing between them, not Redis.
/// </summary>
public class RedisFirstFixedWindowLimiterTests
{
    [Fact]
    public async Task Redis_Answers_When_It_Is_Reachable()
    {
        var redis = new RecordingLimiter(acquired: false);
        var local = new RecordingLimiter(acquired: true);
        await using var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => true, _ => { });

        using var lease = await limiter.AcquireAsync();

        lease.IsAcquired.ShouldBeFalse("the Redis limiter's verdict, not the local one's, is what came back");
        redis.Calls.ShouldBe(1);
        local.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(typeof(RedisConnectionException))]
    [InlineData(typeof(RedisTimeoutException))]
    public async Task A_Redis_Failure_Falls_Back_To_The_Local_Window_And_Reports_It(Type exceptionType)
    {
        Exception failure = exceptionType == typeof(RedisConnectionException)
            ? new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down")
            : new RedisTimeoutException("slow", CommandStatus.Unknown);
        var redis = new RecordingLimiter(acquired: true, throwing: failure);
        var local = new RecordingLimiter(acquired: true);
        Exception? reported = null;
        await using var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => true, ex => reported = ex);

        using var lease = await limiter.AcquireAsync();

        lease.IsAcquired.ShouldBeTrue();
        redis.Calls.ShouldBe(1);
        local.Calls.ShouldBe(1);
        reported.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task A_Known_Disconnected_Multiplexer_Is_Not_Even_Asked()
    {
        var redis = new RecordingLimiter(acquired: true);
        var local = new RecordingLimiter(acquired: true);
        await using var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => false, _ => { });

        using var lease = await limiter.AcquireAsync();

        redis.Calls.ShouldBe(0);
        local.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Any_Other_Exception_Still_Surfaces()
    {
        // Only a Redis outage is a reason to count locally; a bug is a bug.
        var redis = new RecordingLimiter(acquired: true, throwing: new InvalidOperationException("bug"));
        var local = new RecordingLimiter(acquired: true);
        await using var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => true, _ => { });

        await Should.ThrowAsync<InvalidOperationException>(async () => await limiter.AcquireAsync());
        local.Calls.ShouldBe(0);
    }

    /// <summary>PartitionedRateLimiter only evicts a partition whose limiter reports an idle
    /// duration; the partitions are keyed by IP, so one that never reports idle is a dictionary
    /// that grows with every address that ever called.</summary>
    [Fact]
    public async Task Reports_Idle_After_A_Request_And_Busy_During_One()
    {
        var gate = new TaskCompletionSource<RateLimitLease>();
        var redis = new RecordingLimiter(acquired: true, pending: gate.Task);
        await using var limiter = new RedisFirstFixedWindowLimiter(redis, new RecordingLimiter(acquired: true), () => true, _ => { });

        limiter.IdleDuration.ShouldNotBeNull();

        var inFlight = limiter.AcquireAsync().AsTask();
        limiter.IdleDuration.ShouldBeNull("a request is in flight");

        gate.SetResult(new Lease(acquired: true));
        (await inFlight).IsAcquired.ShouldBeTrue();
        limiter.IdleDuration.ShouldNotBeNull();
        limiter.IdleDuration!.Value.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    /// <summary>The middleware tries the synchronous acquire first and only goes async when it is
    /// refused; a synchronous grant from the local window would bypass Redis for the first
    /// PermitLimit requests of every window.</summary>
    [Fact]
    public void The_Synchronous_Path_Always_Defers_To_The_Async_One()
    {
        var redis = new RecordingLimiter(acquired: true);
        var local = new RecordingLimiter(acquired: true);
        using var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => true, _ => { });

        using var lease = limiter.AttemptAcquire();

        lease.IsAcquired.ShouldBeFalse();
        redis.Calls.ShouldBe(0);
        local.Calls.ShouldBe(0);
    }

    [Fact]
    public void Disposing_Disposes_Both()
    {
        var redis = new RecordingLimiter(acquired: true);
        var local = new RecordingLimiter(acquired: true);
        var limiter = new RedisFirstFixedWindowLimiter(redis, local, () => true, _ => { });

        limiter.Dispose();

        redis.Disposed.ShouldBeTrue();
        local.Disposed.ShouldBeTrue();
    }

    private sealed class RecordingLimiter(bool acquired, Exception? throwing = null, Task<RateLimitLease>? pending = null) : RateLimiter
    {
        public int Calls { get; private set; }

        public bool Disposed { get; private set; }

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public override RateLimiterStatistics? GetStatistics() => null;

        protected override RateLimitLease AttemptAcquireCore(int permitCount) => new Lease(acquired);

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            Calls++;
            if (throwing is not null)
            {
                throw throwing;
            }

            return pending is not null ? await pending : new Lease(acquired);
        }

        protected override void Dispose(bool disposing) => Disposed = true;
    }

    private sealed class Lease(bool acquired) : RateLimitLease
    {
        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
