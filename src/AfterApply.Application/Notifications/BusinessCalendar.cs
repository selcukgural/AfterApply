using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Notifications;

/// <summary>
/// Which days a follow-up can reasonably land on: not a weekend, and — for a company in Türkiye or
/// of unknown country, which is most of them — not a Turkish public holiday. A reminder that falls
/// due on such a day is held back to the next working day, 09:00 Istanbul, and says why.
/// </summary>
/// <remarks>
/// <para>The fixed holidays are the ones Law 2429 names as full days. Arife days and 28 October are
/// half days that start at 13:00, so their mornings — when the reminder would appear — are working
/// time, and they are not listed.</para>
/// <para>The two religious feasts move with the lunar calendar. Their dates are Diyanet's official
/// lists (vakithesaplama.diyanet.gov.tr, "Dini Günler Listesi" for each year), checked 2026-09-27.
/// A year past the table falls back to weekends and the fixed holidays only; a unit test fails
/// while the table does not reach the year after next, so it gets extended in time.</para>
/// </remarks>
public static class BusinessCalendar
{
    public static readonly TimeZoneInfo SiteTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>A deferred reminder appears at this local time on the next working day.</summary>
    public static readonly TimeOnly ShowAt = new(9, 0);

    private static readonly Dictionary<(int Month, int Day), ReminderDeferral> FixedHolidays = new()
    {
        [(1, 1)] = ReminderDeferral.NewYear,
        [(4, 23)] = ReminderDeferral.NationalSovereigntyDay,
        [(5, 1)] = ReminderDeferral.LabourDay,
        [(5, 19)] = ReminderDeferral.YouthDay,
        [(7, 15)] = ReminderDeferral.DemocracyDay,
        [(8, 30)] = ReminderDeferral.VictoryDay,
        [(10, 29)] = ReminderDeferral.RepublicDay
    };

    /// <summary>First day and length of each feast, per Diyanet.</summary>
    private static readonly (DateOnly First, int Days, ReminderDeferral Feast)[] Feasts =
    [
        (new DateOnly(2026, 3, 20), 3, ReminderDeferral.RamadanFeast),
        (new DateOnly(2026, 5, 27), 4, ReminderDeferral.SacrificeFeast),
        (new DateOnly(2027, 3, 9), 3, ReminderDeferral.RamadanFeast),
        (new DateOnly(2027, 5, 16), 4, ReminderDeferral.SacrificeFeast),
        (new DateOnly(2028, 2, 26), 3, ReminderDeferral.RamadanFeast),
        (new DateOnly(2028, 5, 5), 4, ReminderDeferral.SacrificeFeast),
        (new DateOnly(2029, 2, 14), 3, ReminderDeferral.RamadanFeast),
        (new DateOnly(2029, 4, 24), 4, ReminderDeferral.SacrificeFeast),
        (new DateOnly(2030, 2, 4), 3, ReminderDeferral.RamadanFeast),
        (new DateOnly(2030, 4, 13), 4, ReminderDeferral.SacrificeFeast)
    ];

    /// <summary>The last year the feast table covers.</summary>
    public static int CoveredThroughYear => Feasts.Max(f => f.First.Year);

    /// <summary>Whether a company's Turkish holidays apply: in Türkiye, or country unknown (the
    /// product's audience applies to Turkish companies, and most rows have no country yet).</summary>
    public static bool UsesTurkishHolidays(string? companyCountry) =>
        string.IsNullOrWhiteSpace(companyCountry) || companyCountry.Equals("TR", StringComparison.OrdinalIgnoreCase);

    public static ReminderDeferral? HolidayOn(DateOnly day)
    {
        if (FixedHolidays.TryGetValue((day.Month, day.Day), out var holiday))
        {
            return holiday;
        }

        foreach (var (first, days, feast) in Feasts)
        {
            if (day >= first && day < first.AddDays(days))
            {
                return feast;
            }
        }

        return null;
    }

    /// <summary>Why <paramref name="day"/> is not a working day, or null when it is.</summary>
    public static ReminderDeferral? ClosedBecause(DateOnly day, bool turkishHolidays)
    {
        if (turkishHolidays && HolidayOn(day) is { } holiday)
        {
            return holiday;
        }

        return day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? ReminderDeferral.Weekend : null;
    }

    /// <summary>
    /// When a reminder created at <paramref name="now"/> should appear, and why, or null when now
    /// is already a working day. The reason names a holiday whenever one falls in the skipped
    /// days — "moved because of the feast" says more than "moved because of the weekend" when a
    /// feast runs into one.
    /// </summary>
    public static (DateTimeOffset Until, ReminderDeferral Reason)? DeferralFor(DateTimeOffset now, bool turkishHolidays)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, SiteTimeZone).DateTime);
        if (ClosedBecause(day, turkishHolidays) is not { } first)
        {
            return null;
        }

        var reason = first;
        var next = day.AddDays(1);
        while (ClosedBecause(next, turkishHolidays) is { } closed)
        {
            if (reason == ReminderDeferral.Weekend && closed != ReminderDeferral.Weekend)
            {
                reason = closed;
            }

            next = next.AddDays(1);
        }

        var local = next.ToDateTime(ShowAt);
        return (new DateTimeOffset(local, SiteTimeZone.GetUtcOffset(local)), reason);
    }
}
