using System.Globalization;

namespace AfterApply.Application.JobSources;

/// <summary>
/// ISO 8601 year-week as one integer, <c>yyyyWW</c> — 2026-09-14 (a Monday) is 202638. The weekly
/// cap and the run summary are keyed on it so "this week" means the same thing on every instance
/// and does not depend on when in the week the job actually ran.
/// </summary>
public static class WeekKey
{
    public static int From(DateTimeOffset moment)
    {
        var date = moment.UtcDateTime.Date;
        var year = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        return year * 100 + week;
    }

    public static bool IsValid(int weekKey) => weekKey is >= 200001 and <= 210053 && weekKey % 100 is >= 1 and <= 53;
}
