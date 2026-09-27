using System.Collections.Frozen;
using AfterApply.Application.FeatureFlags;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace AfterApply.Infrastructure.FeatureFlags;

/// <summary><c>FeatureFlags</c> configuration section.</summary>
public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>How often each instance re-reads the overrides while its Redis subscription is
    /// healthy. Changes arrive by announcement within a second or two; this read is only the
    /// insurance against one announcement lost in a blip nobody noticed.</summary>
    public int PollSeconds { get; init; } = 300;

    /// <summary>How often it re-reads while the subscription is down or not yet made — then
    /// polling is the only way a change arrives, so this bounds how stale a flag can be.</summary>
    public int DegradedPollSeconds { get; init; } = 15;
}

/// <summary>
/// When the next unannounced read is due: rarely while this instance hears the Redis
/// announcements, often while it cannot. Starts degraded — nothing is subscribed yet.
/// </summary>
public sealed class FeatureFlagPollSchedule(FeatureFlagOptions options)
{
    private volatile bool _hearing;

    public bool IsHearingAnnouncements => _hearing;

    public TimeSpan NextPoll => TimeSpan.FromSeconds(Math.Max(1, _hearing ? options.PollSeconds : options.DegradedPollSeconds));

    public void Subscribed() => _hearing = true;

    public void SubscriptionLost() => _hearing = false;
}

/// <summary>
/// The overrides every instance holds in memory (DECISIONS.md 2026-09-27, runtime flags). The
/// database is the truth; Redis only tells the other instances to re-read it. A flag read is a
/// dictionary lookup, so it stays synchronous at the ~45 places that used to read
/// <c>IOptions&lt;T&gt;.Value.Enabled</c>.
///
/// When the database cannot be read the last snapshot stays in force, and before the first
/// successful read that is the empty one — every flag on its deploy default, which is exactly
/// how the product ran before runtime flags existed.
/// </summary>
public sealed class FeatureFlagStore(
    FeatureFlagCatalog catalog,
    IServiceScopeFactory scopes,
    IFusionCache cache,
    ILogger<FeatureFlagStore> logger) : IFeatureFlags
{
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private volatile FrozenDictionary<FeatureFlag, bool> _overrides = FrozenDictionary<FeatureFlag, bool>.Empty;
    private bool _loaded;

    /// <summary>Raised after every successful reload — for the tests that wait on another
    /// instance to pick a change up.</summary>
    public event Action? Reloaded;

    public bool IsEnabled(FeatureFlag flag) =>
        _overrides.TryGetValue(flag, out var enabled) ? enabled : catalog.DefaultOf(flag);

    /// <summary>The override in force on this instance, if any.</summary>
    public bool? OverrideOf(FeatureFlag flag) => _overrides.TryGetValue(flag, out var enabled) ? enabled : null;

    /// <summary>
    /// Re-reads every override. Serialised, so a slow read that started earlier can never
    /// overwrite the result of one that started later. Throws when the database does; the
    /// snapshot is then left as it was.
    ///
    /// When the overrides actually changed (not on the first read after start-up), the whole
    /// application cache is cleared on every instance: cached pages were computed under the old
    /// flags — the company directory with or without salaries, a company page with or without its
    /// tabs — and would otherwise outlive the switch by their TTL. Each instance clears after its
    /// own re-read, so an entry recomputed by an instance that had not re-read yet is cleared again
    /// by that instance a moment later. Switches are rare; one cold cache is the price.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await dbContext.FeatureFlagOverrides.AsNoTracking()
                .Select(o => new { o.Key, o.Enabled })
                .ToListAsync(cancellationToken);

            var overrides = new Dictionary<FeatureFlag, bool>();
            foreach (var row in rows)
            {
                if (FeatureFlagNames.TryParse(row.Key, out var flag))
                {
                    overrides[flag] = row.Enabled;
                }
                else
                {
                    // A row a newer build wrote, or one whose member was removed: ignored rather
                    // than guessed at, and the flag it named (if any) runs on its default.
                    logger.LogWarning("Ignoring feature flag override with unknown key {Key}", row.Key);
                }
            }

            var previous = _overrides;
            _overrides = overrides.ToFrozenDictionary();
            var changed = _loaded && !SameOverrides(previous, _overrides);
            _loaded = true;

            if (changed)
            {
                await ClearCacheAsync(cancellationToken);
            }
        }
        finally
        {
            _reloadGate.Release();
        }

        Reloaded?.Invoke();
    }

    private static bool SameOverrides(FrozenDictionary<FeatureFlag, bool> a, FrozenDictionary<FeatureFlag, bool> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private async Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cache.ClearAsync(allowFailSafe: false, token: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The flag itself is already switched; cached pages then catch up within their TTL.
            logger.LogWarning(ex, "Application cache not cleared after a feature flag change");
        }
    }
}

/// <summary>
/// The Redis pub/sub channel that tells every instance "an override changed, re-read". Carries no
/// data — the database stays the only source — so a lost or duplicated message costs at most one
/// poll interval or one extra read.
/// </summary>
public sealed class FeatureFlagChannel(IConnectionMultiplexer redis, IOptions<CachingOptions> caching, ILogger<FeatureFlagChannel> logger)
{
    // Pub/sub is not scoped by Redis database, so the channel carries the same prefix as the
    // cache backplane: two deployments (or two test classes) on one Redis never hear each other.
    private RedisChannel Channel => RedisChannel.Literal($"{caching.Value.ChannelPrefix}:feature-flags");

    private EventHandler<ConnectionFailedEventArgs>? _failed;
    private EventHandler<ConnectionFailedEventArgs>? _restored;

    public async Task SubscribeAsync(Action onChanged)
    {
        await redis.GetSubscriber().SubscribeAsync(Channel, (_, _) => onChanged());
    }

    public async Task UnsubscribeAsync()
    {
        await redis.GetSubscriber().UnsubscribeAsync(Channel);
    }

    /// <summary>
    /// Reports the pub/sub connection going and coming back. StackExchange.Redis re-subscribes on
    /// its own after a reconnect, but whatever was published in between is gone — pub/sub keeps
    /// nothing — so the caller polls often while it is down and reads once when it is back.
    /// </summary>
    public void WatchConnection(Action onLost, Action onRestored)
    {
        _failed = (_, e) =>
        {
            if (e.ConnectionType == ConnectionType.Subscription)
            {
                onLost();
            }
        };
        _restored = (_, e) =>
        {
            if (e.ConnectionType == ConnectionType.Subscription)
            {
                onRestored();
            }
        };
        redis.ConnectionFailed += _failed;
        redis.ConnectionRestored += _restored;
    }

    public void StopWatching()
    {
        if (_failed is not null)
        {
            redis.ConnectionFailed -= _failed;
        }

        if (_restored is not null)
        {
            redis.ConnectionRestored -= _restored;
        }
    }

    /// <summary>Best effort: when Redis is unreachable the other instances pick the change up on
    /// their next poll instead.</summary>
    public async Task PublishAsync()
    {
        try
        {
            await redis.GetSubscriber().PublishAsync(Channel, "changed");
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Feature flag change not broadcast; other instances will see it on their next poll");
        }
    }
}
