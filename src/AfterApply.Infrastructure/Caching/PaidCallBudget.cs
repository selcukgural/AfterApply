using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AfterApply.Infrastructure.Caching;

/// <summary>Daily limits on calls that cost money per call (2026-09-24), bound from <c>PaidCalls</c>.</summary>
public sealed class PaidCallOptions
{
    public const string SectionName = "PaidCalls";

    /// <summary>The OpenAI calls behind Gmail scanning: classifying a message the rules could not
    /// place, and reading the reason out of a rejection. One budget for both.</summary>
    public DailyBudget EmailSignals { get; init; } = new() { GlobalDaily = 1000, PerUserDaily = 60 };

    public sealed class DailyBudget
    {
        /// <summary>Calls the whole product makes in a UTC day.</summary>
        public int GlobalDaily { get; init; }

        /// <summary>Calls one account's activity may cause in a UTC day; 0 means no per-account cap.</summary>
        public int PerUserDaily { get; init; }
    }
}

/// <summary>
/// Reserves one paid call against a daily budget before the call is made (2026-09-24). Counting
/// the rows a feature wrote afterwards — what the CV scan used to do — lets concurrent requests all
/// read the same count and all pass; an atomic Redis increment cannot. The count lives in Redis
/// under a key per UTC day that expires by itself, so every instance shares one budget.
///
/// Redis being unreachable refuses the call: the features that use this all have a free fallback
/// (rule-based classification, a scan without content notes), and a paid call nobody can count is
/// the one this exists to prevent.
/// </summary>
public sealed class PaidCallBudget(IConnectionMultiplexer redis, IOptions<CachingOptions> caching,
    ILogger<PaidCallBudget> logger, TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromDays(2);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>True when the call may go ahead — and it is then counted.</summary>
    public async Task<bool> TryReserveAsync(string feature, int globalDaily, Guid? userId = null, int perUserDaily = 0)
    {
        var day = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd");
        var prefix = $"{caching.Value.KeyPrefix}paid-call:{feature}:{day}";

        try
        {
            var db = redis.GetDatabase();

            if (userId is { } id && perUserDaily > 0 && !await WithinAsync(db, $"{prefix}:user:{id:N}", perUserDaily))
            {
                return false;
            }

            if (!await WithinAsync(db, $"{prefix}:all", globalDaily))
            {
                logger.LogWarning("Daily ceiling of {Ceiling} paid {Feature} calls reached; not calling today", globalDaily, feature);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Paid-call budget for {Feature} unavailable; not calling", feature);
            return false;
        }
    }

    /// <summary>
    /// True the first time <paramref name="id"/> is seen for <paramref name="feature"/> within
    /// <paramref name="window"/>: the same input sent again is dropped before it can pay for the
    /// same call twice. Redis being unreachable says "first" — the budget itself still refuses the
    /// call in that case, so nothing is spent unseen.
    /// </summary>
    public async Task<bool> IsFirstSightingAsync(string feature, string id, TimeSpan window)
    {
        try
        {
            return await redis.GetDatabase().StringSetAsync(
                $"{caching.Value.KeyPrefix}paid-call-seen:{feature}:{id}", "1", window, When.NotExists);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Replay check for {Feature} unavailable", feature);
            return true;
        }
    }

    /// <summary>Undoes <see cref="IsFirstSightingAsync"/> for an input whose processing failed, so the
    /// retry is not mistaken for a replay.</summary>
    public async Task ForgetSightingAsync(string feature, string id)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync($"{caching.Value.KeyPrefix}paid-call-seen:{feature}:{id}");
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Replay check for {Feature} could not be reset", feature);
        }
    }

    // A refused call still increments, which only makes the counter overshoot past a limit that is
    // already reached — harmless, and it keeps the check a single round trip.
    private static async Task<bool> WithinAsync(IDatabase db, string key, int limit)
    {
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, KeyLifetime);
        }

        return count <= limit;
    }
}
