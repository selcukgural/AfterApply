using AfterApply.Application.Benchmark;
using Shouldly;

namespace AfterApply.UnitTests.Benchmark;

public class BenchmarkCalculationsTests
{
    private static List<double> Rates(int count, double value) => Enumerable.Repeat(value, count).ToList();

    [Fact]
    public void A_Reply_Rate_Is_Replies_Over_Applications()
    {
        BenchmarkCalculations.ReplyRate(100, 20).ShouldBe(20.0);
        BenchmarkCalculations.ReplyRate(3, 1).ShouldBe(33.3);
        BenchmarkCalculations.ReplyRate(40, 0).ShouldBe(0);
    }

    [Fact]
    public void A_Zero_Denominator_Is_Zero_Rather_Than_A_Division_By_Zero()
    {
        // The validator rejects it, so this is the belt to that braces — a NaN would travel all the
        // way to the page and render as "NaN%".
        BenchmarkCalculations.ReplyRate(0, 0).ShouldBe(0);
        BenchmarkCalculations.ReplyRate(-5, 2).ShouldBe(0);
    }

    [Fact]
    public void The_Median_Handles_Both_Parities_And_An_Empty_Set()
    {
        BenchmarkCalculations.Median([10, 50, 90]).ShouldBe(50);
        BenchmarkCalculations.Median([20, 40, 60, 80]).ShouldBe(50, 1e-12);
        BenchmarkCalculations.Median([42.5]).ShouldBe(42.5);
        BenchmarkCalculations.Median([]).ShouldBe(0);
    }

    [Fact]
    public void The_Median_Does_Not_Depend_On_The_Order_It_Was_Given_In()
    {
        BenchmarkCalculations.Median([90, 10, 50]).ShouldBe(50);
    }

    // The rule the whole feature turns on.
    [Fact]
    public void Below_The_Sample_Threshold_Nothing_Is_Compared()
    {
        var comparison = BenchmarkCalculations.Compare(25, Rates(29, 10), Rates(29, 10), minimumSampleSize: 30);

        comparison.Scope.ShouldBe(BenchmarkComparisonScope.None);
        comparison.MedianRate.ShouldBeNull();
        comparison.ShareBelowYou.ShouldBeNull();
        comparison.ComparedAgainstCount.ShouldBeNull();

        // What is still said: their own number, how many are in the cell, how many have answered
        // overall. Withholding the comparison must not mean withholding the participation count —
        // that number is what tells a reader the answer is coming.
        comparison.YourRate.ShouldBe(25);
        comparison.SampleSize.ShouldBe(29);
        comparison.TotalSubmissions.ShouldBe(29);
    }

    [Fact]
    public void At_The_Threshold_Exactly_The_Comparison_Appears()
    {
        var comparison = BenchmarkCalculations.Compare(25, Rates(30, 10), Rates(30, 10), minimumSampleSize: 30);

        comparison.Scope.ShouldBe(BenchmarkComparisonScope.Sector);
        comparison.ComparedAgainstCount.ShouldBe(30);
        comparison.MedianRate.ShouldBe(10);
        comparison.ShareBelowYou.ShouldBe(100);
    }

    [Fact]
    public void The_Share_Below_Counts_Strictly_Below()
    {
        // Ten answers, four of them lower than this person's, three identical, three higher.
        List<double> cell = [10, 10, 20, 20, 30, 30, 30, 40, 50, 60];

        var comparison = BenchmarkCalculations.Compare(30, cell, cell, minimumSampleSize: 5);

        // Identical answers do not count as "below you" — otherwise everyone reporting the same
        // rate would each be told they beat the others.
        comparison.ShareBelowYou.ShouldBe(40);
        comparison.MedianRate.ShouldBe(30);
    }

    [Fact]
    public void An_Extreme_Answer_Barely_Moves_The_Median()
    {
        // Why the median and not the mean: with no identity behind a submission, one person can
        // answer repeatedly and a joke answer can be sent on purpose. Here 30 ordinary answers get
        // three absurd ones added.
        var honest = Rates(30, 20);
        var polluted = honest.Concat(Rates(3, 100)).ToList();

        var before = BenchmarkCalculations.Compare(20, honest, honest, 30).MedianRate;
        var after = BenchmarkCalculations.Compare(20, polluted, polluted, 30).MedianRate;

        before.ShouldBe(20);
        after.ShouldBe(20);

        // The mean, for contrast, would have moved by roughly seven points.
        polluted.Average().ShouldBeGreaterThan(26);
    }

    [Fact]
    public void The_Answer_Just_Given_Is_Part_Of_The_Sample_It_Is_Compared_Against()
    {
        // The service reads the cell back after saving, so the count the reader is told ("one of
        // n") includes them. A comparison built from everyone else would make the copy wrong.
        var cell = Rates(29, 10).Append(90).ToList();

        var comparison = BenchmarkCalculations.Compare(90, cell, cell, minimumSampleSize: 30);

        comparison.SampleSize.ShouldBe(30);
        comparison.Scope.ShouldBe(BenchmarkComparisonScope.Sector);
        comparison.ShareBelowYou.ShouldBe(96.7);
    }

    [Fact]
    public void A_Sector_Short_Of_The_Threshold_Falls_Back_To_Everyone()
    {
        // The team's call (2026-09-08): "come back in a month" is a poor answer to the only
        // question the page exists for. Four answers in this sector, sixty overall.
        var cell = Rates(4, 12);
        var all = cell.Concat(Rates(56, 22)).ToList();

        var comparison = BenchmarkCalculations.Compare(15, cell, all, minimumSampleSize: 30);

        comparison.Scope.ShouldBe(BenchmarkComparisonScope.Overall);
        comparison.MedianRate.ShouldBe(22);
        // The count quoted beside the median is the pool it came from, not the sector's.
        comparison.ComparedAgainstCount.ShouldBe(60);
        // ...while the sector's own size is still reported, because it is what says how far off a
        // sector median is.
        comparison.SampleSize.ShouldBe(4);
    }

    [Fact]
    public void The_Sector_Wins_Whenever_It_Can_Stand_On_Its_Own()
    {
        // The fallback must never override a real sector median, even when the overall pool is far
        // larger — the sector is the only pool that answers the question actually asked.
        var cell = Rates(30, 12);
        var all = cell.Concat(Rates(500, 40)).ToList();

        var comparison = BenchmarkCalculations.Compare(15, cell, all, minimumSampleSize: 30);

        comparison.Scope.ShouldBe(BenchmarkComparisonScope.Sector);
        comparison.MedianRate.ShouldBe(12);
        comparison.ComparedAgainstCount.ShouldBe(30);
    }

    [Fact]
    public void With_Too_Little_Of_Everything_Nothing_Is_Claimed_At_All()
    {
        var cell = Rates(2, 12);
        var all = Rates(9, 20);

        var comparison = BenchmarkCalculations.Compare(15, cell, all, minimumSampleSize: 30);

        comparison.Scope.ShouldBe(BenchmarkComparisonScope.None);
        comparison.MedianRate.ShouldBeNull();
        comparison.ComparedAgainstCount.ShouldBeNull();
        comparison.TotalSubmissions.ShouldBe(9);
    }
}
