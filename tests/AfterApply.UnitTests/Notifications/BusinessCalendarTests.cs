using AfterApply.Application.Notifications;
using AfterApply.Domain.Notifications;
using Shouldly;

namespace AfterApply.UnitTests.Notifications;

/// <summary>
/// Which mornings a follow-up may land on. The feast dates are Diyanet's official lists; the
/// cases below pin the ones that make the rules interesting (a feast running into a weekend, a
/// Monday holiday after a weekend).
/// </summary>
public class BusinessCalendarTests
{
    private static readonly TimeSpan Istanbul = TimeSpan.FromHours(3);

    private static DateTimeOffset At(int y, int m, int d, int h = 6) => new(y, m, d, h, 0, 0, Istanbul);

    [Fact]
    public void A_Working_Day_Is_Not_Deferred()
    {
        BusinessCalendar.DeferralFor(At(2026, 9, 29), turkishHolidays: true).ShouldBeNull(); // Tuesday
    }

    [Fact]
    public void A_Saturday_Moves_To_Monday_Morning()
    {
        var deferral = BusinessCalendar.DeferralFor(At(2026, 10, 3), turkishHolidays: true)!.Value; // Saturday

        deferral.Until.ShouldBe(At(2026, 10, 5, 9));
        deferral.Reason.ShouldBe(ReminderDeferral.Weekend);
    }

    [Fact]
    public void A_Feast_Running_Into_A_Weekend_Is_Named_As_The_Feast()
    {
        // Ramazan Bayramı 2026: Fri 20 – Sun 22 March.
        var deferral = BusinessCalendar.DeferralFor(At(2026, 3, 20), turkishHolidays: true)!.Value;

        deferral.Until.ShouldBe(At(2026, 3, 23, 9));
        deferral.Reason.ShouldBe(ReminderDeferral.RamadanFeast);
    }

    [Fact]
    public void A_Weekend_Followed_By_A_Holiday_Monday_Names_The_Holiday()
    {
        // Ramazan Bayramı 2028: Sat 26 – Mon 28 February.
        var deferral = BusinessCalendar.DeferralFor(At(2028, 2, 26), turkishHolidays: true)!.Value;

        deferral.Until.ShouldBe(At(2028, 2, 29, 9));
        deferral.Reason.ShouldBe(ReminderDeferral.RamadanFeast);
    }

    [Fact]
    public void The_Sacrifice_Feast_Spans_Four_Days()
    {
        // Kurban Bayramı 2026: Wed 27 – Sat 30 May; Sunday 31 follows.
        var deferral = BusinessCalendar.DeferralFor(At(2026, 5, 27), turkishHolidays: true)!.Value;

        deferral.Until.ShouldBe(At(2026, 6, 1, 9));
        deferral.Reason.ShouldBe(ReminderDeferral.SacrificeFeast);
    }

    [Fact]
    public void An_Arife_Morning_Is_A_Working_Morning()
    {
        BusinessCalendar.DeferralFor(At(2026, 5, 26), turkishHolidays: true).ShouldBeNull();
    }

    [Theory]
    [InlineData(2026, 10, 29, ReminderDeferral.RepublicDay)]
    [InlineData(2027, 1, 1, ReminderDeferral.NewYear)]
    [InlineData(2027, 4, 23, ReminderDeferral.NationalSovereigntyDay)]
    [InlineData(2027, 7, 15, ReminderDeferral.DemocracyDay)]
    [InlineData(2029, 4, 24, ReminderDeferral.SacrificeFeast)]
    [InlineData(2030, 2, 4, ReminderDeferral.RamadanFeast)]
    public void Holidays_Are_Recognised(int y, int m, int d, ReminderDeferral expected)
    {
        BusinessCalendar.HolidayOn(new DateOnly(y, m, d)).ShouldBe(expected);
    }

    [Fact]
    public void A_Company_Outside_Turkey_Keeps_Only_The_Weekend()
    {
        BusinessCalendar.DeferralFor(At(2026, 10, 29), turkishHolidays: false).ShouldBeNull(); // Thursday
        BusinessCalendar.DeferralFor(At(2026, 10, 31), turkishHolidays: false)!.Value.Reason.ShouldBe(ReminderDeferral.Weekend);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("TR", true)]
    [InlineData("tr", true)]
    [InlineData("NL", false)]
    [InlineData("AE", false)]
    public void Turkish_Holidays_Apply_In_Turkey_Or_When_The_Country_Is_Unknown(string? country, bool expected)
    {
        BusinessCalendar.UsesTurkishHolidays(country).ShouldBe(expected);
    }

    [Fact]
    public void The_Istanbul_Day_Decides_Not_The_Utc_One()
    {
        // 23:30 UTC on Friday is 02:30 Saturday in Istanbul.
        var deferral = BusinessCalendar.DeferralFor(new DateTimeOffset(2026, 10, 2, 23, 30, 0, TimeSpan.Zero), turkishHolidays: true);

        deferral.ShouldNotBeNull();
        deferral.Value.Until.ShouldBe(At(2026, 10, 5, 9));
    }

    [Fact]
    public void The_Feast_Table_Reaches_The_Year_After_Next()
    {
        // A tripwire, on purpose: the feasts move every year and Diyanet publishes years ahead.
        // When this fails, add the next years' dates from vakithesaplama.diyanet.gov.tr.
        BusinessCalendar.CoveredThroughYear.ShouldBeGreaterThanOrEqualTo(DateTime.UtcNow.Year + 1);
    }
}
