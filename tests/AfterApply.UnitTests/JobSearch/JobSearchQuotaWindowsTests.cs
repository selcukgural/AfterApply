using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

public class JobSearchQuotaWindowsTests
{
    [Fact]
    public void Day_Start_Is_Midnight_Utc_Whatever_The_Offset()
    {
        var istanbulEvening = new DateTimeOffset(2026, 9, 12, 1, 30, 0, TimeSpan.FromHours(3)); // 22:30 UTC on the 11th

        JobSearchQuotaWindows.DayStart(istanbulEvening).ShouldBe(new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(2026, 9, 12, 15, 2026, 8, 15)]   // before this month's reset day → last month's
    [InlineData(2026, 9, 15, 15, 2026, 9, 15)]   // on the reset day → today
    [InlineData(2026, 9, 20, 15, 2026, 9, 15)]   // after → this month's
    [InlineData(2026, 9, 12, 1, 2026, 9, 1)]     // calendar month
    [InlineData(2026, 3, 1, 28, 2026, 2, 28)]    // February always has the 28th
    [InlineData(2026, 3, 1, 31, 2026, 2, 28)]    // 31 is clamped to 28
    public void Month_Start_Is_The_Most_Recent_Reset_Day(int y, int m, int d, int resetDay, int ey, int em, int ed)
    {
        var now = new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero);

        JobSearchQuotaWindows.MonthStart(now, resetDay).ShouldBe(new DateTimeOffset(ey, em, ed, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Month_End_Is_One_Month_After_The_Start()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        JobSearchQuotaWindows.MonthEnd(now, 15).ShouldBe(new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
    }
}
