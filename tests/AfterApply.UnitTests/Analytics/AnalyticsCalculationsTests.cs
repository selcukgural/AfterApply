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

    // 2026-09-05 is a Saturday; its Monday-start week opens on 2026-08-31.
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildWeeklyBuckets_With_Zero_Weeks_Returns_Empty()
    {
        AnalyticsCalculations.BuildWeeklyBuckets([Now], Now, weeks: 0).ShouldBeEmpty();
    }

    [Fact]
    public void BuildWeeklyBuckets_Emits_One_Bucket_Per_Week_Ending_With_The_Current_Week()
    {
        var buckets = AnalyticsCalculations.BuildWeeklyBuckets([], Now, weeks: 12);

        buckets.Count.ShouldBe(12);
        buckets[^1].WeekStart.ShouldBe(new DateOnly(2026, 8, 31));
        buckets[0].WeekStart.ShouldBe(new DateOnly(2026, 6, 15));
        buckets.ShouldAllBe(b => b.Count == 0);
    }

    [Fact]
    public void BuildWeeklyBuckets_Keeps_Empty_Weeks_So_The_Scale_Stays_Constant()
    {
        // One application this week, one three weeks back, nothing in between.
        var buckets = AnalyticsCalculations.BuildWeeklyBuckets(
            [Now.AddDays(-1), Now.AddDays(-21)], Now, weeks: 4);

        buckets.Select(b => b.Count).ShouldBe([1, 0, 0, 1]);
    }

    [Fact]
    public void BuildWeeklyBuckets_Groups_A_Whole_Monday_To_Sunday_Week_Together()
    {
        // Monday 2026-08-31 00:00 through Sunday 2026-09-06 23:59 all land in the last bucket.
        var mondayStart = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        var sundayEnd = new DateTimeOffset(2026, 9, 6, 23, 59, 0, TimeSpan.Zero);

        var buckets = AnalyticsCalculations.BuildWeeklyBuckets(
            [mondayStart, Now, sundayEnd], Now, weeks: 2);

        buckets.Select(b => b.Count).ShouldBe([0, 3]);
    }

    [Fact]
    public void BuildWeeklyBuckets_Drops_Applications_Outside_The_Window()
    {
        // A year old (before the window) and a week into the future (after it).
        var buckets = AnalyticsCalculations.BuildWeeklyBuckets(
            [Now.AddDays(-365), Now.AddDays(7), Now], Now, weeks: 4);

        buckets.Sum(b => b.Count).ShouldBe(1);
    }

    [Fact]
    public void BuildWeeklyBuckets_Buckets_By_Utc_Not_By_The_Original_Offset()
    {
        // Monday 2026-08-31 01:00 at +03:00 is Sunday 2026-08-30 22:00 UTC — the previous week.
        var buckets = AnalyticsCalculations.BuildWeeklyBuckets(
            [new DateTimeOffset(2026, 8, 31, 1, 0, 0, TimeSpan.FromHours(3))], Now, weeks: 2);

        buckets.Select(b => b.Count).ShouldBe([1, 0]);
    }
}
