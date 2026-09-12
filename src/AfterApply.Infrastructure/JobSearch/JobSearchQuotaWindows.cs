namespace AfterApply.Infrastructure.JobSearch;

/// <summary>The two windows the ledger is summed over. Both in UTC; both pure.</summary>
public static class JobSearchQuotaWindows
{
    /// <summary>Midnight UTC of the current day — the per-user daily ceiling's window.</summary>
    public static DateTimeOffset DayStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// The start of the current billing month: the most recent <paramref name="resetDay"/> at
    /// midnight UTC, this month if it has passed and last month otherwise. RapidAPI's quota resets
    /// on the subscription's anniversary, which is why this is not simply the first of the month.
    /// The day is clamped to 1–28 so every month has it.
    /// </summary>
    public static DateTimeOffset MonthStart(DateTimeOffset now, int resetDay)
    {
        var day = Math.Clamp(resetDay, 1, 28);
        var utc = now.ToUniversalTime();
        var candidate = new DateTimeOffset(utc.Year, utc.Month, day, 0, 0, 0, TimeSpan.Zero);
        return candidate <= utc ? candidate : candidate.AddMonths(-1);
    }

    public static DateTimeOffset MonthEnd(DateTimeOffset now, int resetDay) => MonthStart(now, resetDay).AddMonths(1);
}
