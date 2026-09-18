namespace AfterApply.Infrastructure.Caching;

/// <summary>
/// <c>Redis</c> configuration section. The prefixes exist so that several deployments — or, in the
/// integration suite, several test classes — can share one Redis without seeing each other's
/// entries or each other's backplane traffic. Keys are scoped by <see cref="KeyPrefix"/> (and in
/// tests additionally by the connection string's <c>defaultDatabase</c>); pub/sub channels are not
/// database-scoped in Redis at all, so <see cref="ChannelPrefix"/> is the only thing keeping one
/// host's invalidations out of another host's L1.
/// </summary>
public sealed class CachingOptions
{
    public const string SectionName = "Redis";

    public string KeyPrefix { get; set; } = "ek:";

    public string ChannelPrefix { get; set; } = "ek";

    /// <summary>
    /// Block the first cache operation until the backplane subscription is live. Off in production
    /// (a slow Redis must not delay start-up; auto-recovery replays what was missed). On in the
    /// integration suite, where a test writes on one host and reads on another a millisecond later.
    /// </summary>
    public bool WaitForBackplaneSubscribe { get; set; }
}
