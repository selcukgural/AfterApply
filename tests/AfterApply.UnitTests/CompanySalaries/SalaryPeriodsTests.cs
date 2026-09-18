using AfterApply.Domain.CompanySalaries;
using Shouldly;

namespace AfterApply.UnitTests.CompanySalaries;

public class SalaryPeriodsTests
{
    [Theory]
    [InlineData(2026, 2, 2025)]
    [InlineData(2026, 1, 2026)]
    [InlineData(2026, 5, 2022)]
    // A zero or negative window would put the cutoff in the future and make every row previous.
    [InlineData(2026, 0, 2026)]
    public void The_Cutoff_Is_The_First_Year_Inside_The_Window(int year, int window, int cutoff)
    {
        SalaryPeriods.CutoffYear(year, window).ShouldBe(cutoff);
    }

    [Theory]
    [InlineData(2024, null, true)] // still drawing it
    [InlineData(2020, 2026, true)]
    [InlineData(2020, 2025, true)] // ended at the cutoff
    [InlineData(2020, 2024, false)] // ended before it
    [InlineData(2010, 2012, false)]
    [InlineData(null, null, false)] // period never given
    public void A_Row_Is_Current_When_Its_Period_Is_Known_And_Reaches_The_Cutoff(int? start, int? end, bool current)
    {
        SalaryPeriods.IsCurrent(start, end, cutoffYear: 2025).ShouldBe(current);
    }
}
