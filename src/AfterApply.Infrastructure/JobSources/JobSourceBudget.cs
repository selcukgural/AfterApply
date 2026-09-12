namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The two rules that decide whether the sweep may send one more request, as a pure function of
/// what the ledger says: the day's count against the ceiling, and whether the source told us to
/// stop within the cooldown. The sweep reads the ledger once at the start and counts in memory
/// from there, so the check is cheap enough to run before every request.
/// </summary>
public static class JobSourceBudget
{
    public static bool CanSpend(int requestsToday, int maxRequestsPerDay, DateTimeOffset? lastBlockedAt, TimeSpan cooldown,
        DateTimeOffset now) =>
        requestsToday < maxRequestsPerDay && !IsInCooldown(lastBlockedAt, cooldown, now);

    public static DateTimeOffset? CooldownUntil(DateTimeOffset? lastBlockedAt, TimeSpan cooldown) =>
        lastBlockedAt is { } blocked ? blocked + cooldown : null;

    public static bool IsInCooldown(DateTimeOffset? lastBlockedAt, TimeSpan cooldown, DateTimeOffset now) =>
        CooldownUntil(lastBlockedAt, cooldown) is { } until && until > now;
}
