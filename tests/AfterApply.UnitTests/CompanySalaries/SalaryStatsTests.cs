using AfterApply.Application.CompanySalaries;
using Shouldly;

namespace AfterApply.UnitTests.CompanySalaries;

public class SalaryStatsTests
{
    [Fact]
    public void Empty_Has_No_Median()
    {
        SalaryStats.Median([]).ShouldBeNull();
    }

    [Fact]
    public void Odd_Count_Is_The_Middle_Value()
    {
        SalaryStats.Median([90_000m, 48_000m, 62_000m]).ShouldBe(62_000m);
    }

    [Fact]
    public void Even_Count_Averages_The_Two_Middle_Values()
    {
        SalaryStats.Median([130_000m, 48_000m, 62_000m, 95_000m]).ShouldBe(78_500m);
    }

    [Fact]
    public void Rounds_To_Two_Places()
    {
        SalaryStats.Median([1m, 2.005m]).ShouldBe(1.5m);
        SalaryStats.Median([1.111m, 2.222m]).ShouldBe(1.67m);
    }
}
