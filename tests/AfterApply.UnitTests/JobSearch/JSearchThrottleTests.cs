using System.Diagnostics;
using AfterApply.Infrastructure.JobSearch;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

public class JSearchThrottleTests
{
    [Fact]
    public async Task Off_By_Default_And_Free_To_Pass()
    {
        using var throttle = new JSearchThrottle(Options.Create(new JobSearchOptions()));

        throttle.IsEnabled.ShouldBeFalse();
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
        {
            await throttle.WaitAsync(CancellationToken.None);
        }

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Concurrent_Callers_Beyond_The_Per_Second_Budget_Wait_For_The_Next_Second()
    {
        using var throttle = new JSearchThrottle(Options.Create(new JobSearchOptions { RequestsPerSecond = 2 }));
        throttle.IsEnabled.ShouldBeTrue();

        var stopwatch = Stopwatch.StartNew();
        var waits = Enumerable.Range(0, 4).Select(_ => throttle.WaitAsync(CancellationToken.None).AsTask()).ToArray();
        await Task.WhenAll(waits);

        // Two go at once; the other two need the bucket to refill at least once.
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_Cancelled_Caller_Leaves_The_Queue()
    {
        using var throttle = new JSearchThrottle(Options.Create(new JobSearchOptions { RequestsPerSecond = 1 }));
        await throttle.WaitAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Should.ThrowAsync<OperationCanceledException>(() => throttle.WaitAsync(cts.Token).AsTask());
    }
}
