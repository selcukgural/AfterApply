using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AfterApply.Infrastructure.FeatureFlags;

/// <summary>
/// Keeps this instance's <see cref="FeatureFlagStore"/> current: one read before the app starts
/// serving (bounded, so a slow database delays start-up by seconds at most and then the defaults
/// apply), then a re-read whenever another instance announces a change, and an unannounced one on
/// the <see cref="FeatureFlagPollSchedule"/>: every <see cref="FeatureFlagOptions.PollSeconds"/>
/// (5 minutes) while the Redis subscription is healthy, every
/// <see cref="FeatureFlagOptions.DegradedPollSeconds"/> (15 seconds) while it is not, plus one read
/// the moment it comes back — the announcements published while it was down are lost.
/// </summary>
internal sealed class FeatureFlagRefresher(
    FeatureFlagStore store,
    FeatureFlagChannel channel,
    IOptions<FeatureFlagOptions> options,
    ILogger<FeatureFlagRefresher> logger) : IHostedService, IAsyncDisposable
{
    private static readonly TimeSpan InitialReadTimeout = TimeSpan.FromSeconds(5);

    // Capacity one, extra writes dropped: ten announcements while a read is running need one
    // more read, not ten.
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly CancellationTokenSource _stopping = new();
    private readonly FeatureFlagPollSchedule _schedule = new(options.Value);
    private Task? _loop;
    private bool _subscribed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(InitialReadTimeout);
            await TryReloadAsync(timeout.Token);
        }

        channel.WatchConnection(
            onLost: () =>
            {
                _schedule.SubscriptionLost();
                // Wake the loop so the short interval applies from now, not after the long one.
                _changes.Writer.TryWrite(true);
                logger.LogWarning("Feature flag channel lost; polling every {Seconds} s until it is back",
                    options.Value.DegradedPollSeconds);
            },
            onRestored: () =>
            {
                if (_subscribed)
                {
                    _schedule.Subscribed();
                }

                _changes.Writer.TryWrite(true);
            });

        await TrySubscribeAsync();
        _loop = RunAsync(_stopping.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        channel.StopWatching();
        await _stopping.CancelAsync();
        if (_loop is not null)
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        if (_subscribed)
        {
            try
            {
                await channel.UnsubscribeAsync();
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
            {
                // Shutting down; the connection goes with the process.
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // A cancelled wait rather than WaitAsync(timeout): the latter would leave one abandoned
            // waiter on the channel per quiet poll, piling up for as long as nothing changes.
            using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                wait.CancelAfter(_schedule.NextPoll);
                try
                {
                    await _changes.Reader.WaitToReadAsync(wait.Token);
                    _changes.Reader.TryRead(out _);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The poll: nothing announced, read anyway.
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (!_subscribed)
            {
                await TrySubscribeAsync();
            }

            await TryReloadAsync(cancellationToken);
        }
    }

    private async Task TryReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.ReloadAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // The snapshot in force stays; the next announcement or poll tries again.
            logger.LogWarning(ex, "Feature flag overrides could not be read; keeping the current values");
        }
    }

    private async Task TrySubscribeAsync()
    {
        try
        {
            await channel.SubscribeAsync(() => _changes.Writer.TryWrite(true));
            _subscribed = true;
            _schedule.Subscribed();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Retried on every (short) poll; until then changes arrive by polling alone.
            logger.LogWarning(ex, "Feature flag channel subscription failed; relying on polling for now");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _stopping.Dispose();
    }
}
