using AfterApply.Application.Analytics;
using Shouldly;

namespace AfterApply.UnitTests.Analytics;

public class AnalyticsCalculationsTests
{
    [Fact]
    public void CalculateRate_With_Zero_Total_Returns_Zero()
    {
        AnalyticsCalculations.CalculateRate(count: 0, total: 0).ShouldBe(0);
    }

    [Theory]
    [InlineData(63, 100, 63.0)]
    [InlineData(1, 3, 33.3)]
    [InlineData(0, 5, 0.0)]
    public void CalculateRate_Computes_Rounded_Percentage(int count, int total, double expected)
    {
        AnalyticsCalculations.CalculateRate(count, total).ShouldBe(expected);
    }

    [Fact]
    public void Average_With_Empty_Collection_Returns_Null()
    {
        AnalyticsCalculations.Average([]).ShouldBeNull();
    }

    [Fact]
    public void Average_Computes_Rounded_Mean()
    {
        // (4 + 7 + 4 + 12) / 4 = 6.75, rounded to 1 decimal (MidpointRounding.ToEven) = 6.8
        AnalyticsCalculations.Average([4, 7, 4, 12]).ShouldBe(6.8);
    }

    [Fact]
    public void Median_With_Empty_Collection_Returns_Null()
    {
        AnalyticsCalculations.Median([]).ShouldBeNull();
    }

    [Fact]
    public void Median_With_Single_Value_Returns_That_Value()
    {
        AnalyticsCalculations.Median([7]).ShouldBe(7);
    }

    [Fact]
    public void Median_With_Odd_Count_Returns_Middle_Value()
    {
        AnalyticsCalculations.Median([9, 1, 5]).ShouldBe(5);
    }

    [Fact]
    public void Median_With_Even_Count_Returns_Average_Of_Two_Middle_Values()
    {
        AnalyticsCalculations.Median([4, 7, 4, 12]).ShouldBe(5.5);
    }

    [Fact]
    public void Median_Does_Not_Require_PreSorted_Input()
    {
        AnalyticsCalculations.Median([12, 4, 7, 4]).ShouldBe(5.5);
    }
}
