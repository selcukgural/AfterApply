using System.Globalization;

namespace AfterApply.IntegrationTests;

/// <summary>A clock a test moves by hand, so "next week" or "after the order expired" is a call
/// rather than a wait. One per class; <see cref="Set" /> back to the start between tests.</summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public DateTimeOffset Start { get; } = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;

    public void Reset() => _now = Start;

    /// <summary>
    /// Whether moving the clock by <paramref name="by"/> stays inside the current ISO week. The
    /// job-source tests start from the real "now" (their tokens are checked against the wall
    /// clock, so the clock cannot start anywhere else) and step "to tomorrow" expecting the same
    /// week — which no tomorrow of a Sunday is. Those steps ask this first and stop there on a
    /// Sunday (UTC) rather than assert a same-week rule on a new week; the other six days cover
    /// the day rollover. Found 2026-09-20, a Sunday, when both failed on every machine.
    /// </summary>
    public bool StaysInIsoWeek(TimeSpan by)
    {
        var (year, week) = IsoWeek(_now);
        return IsoWeek(_now + by) == (year, week);
    }

    private static (int Year, int Week) IsoWeek(DateTimeOffset moment)
    {
        var date = moment.UtcDateTime.Date;
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }
}
