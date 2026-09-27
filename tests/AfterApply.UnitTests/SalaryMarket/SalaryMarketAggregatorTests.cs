using AfterApply.Application.SalaryMarket;
using AfterApply.Application.SalaryMarket.Import;
using Shouldly;

namespace AfterApply.UnitTests.SalaryMarket;

public class SalaryMarketAggregatorTests
{
    [Theory]
    [InlineData("100.000 - 104.999", 102500, false)]
    [InlineData("5.000 TL - 7.500 TL", 6250, false)]
    [InlineData("15.000 TL ve üzeri", 15000, true)]
    [InlineData("15.000 TL veya daha fazla", 15000, true)]
    [InlineData("2.000 TL ve aşağısı", 1000, false)]
    [InlineData("95000", 95000, false)]
    public void Reads_Every_Salary_Answer_Shape(string text, int value, bool openTop)
    {
        var band = SalaryBand.Parse(text);

        band.ShouldNotBeNull();
        band.Value.ShouldBe(value);
        band.OpenTop.ShouldBe(openTop);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bilmiyorum")]
    [InlineData("0")]
    public void An_Answer_Without_An_Amount_Is_No_Salary(string? text) => SalaryBand.Parse(text).ShouldBeNull();

    private static NormalizedResponse Response(decimal value, SalaryMarketLevel? level = SalaryMarketLevel.Senior,
        SalaryMarketExperience? experience = SalaryMarketExperience.SixToTen, string group = "back-end-developer",
        int year = 2026, bool openTop = false, string source = "onceki-yazilimci") =>
        new(year, source, group, level, experience, new SalaryBand(value, openTop));

    [Fact]
    public void Percentiles_Are_Nearest_Rank_Rounded_To_Five_Hundred()
    {
        // 1 000 … 11 000 TL: nearest rank on eleven values is the value at p·10.
        var bands = Enumerable.Range(1, 11).Select(i => new SalaryBand(i * 1000 + 240, false)).ToList();

        var stats = SalaryMarketAggregator.Stats(bands);

        stats.Count.ShouldBe(11);
        stats.P10.ShouldBe(2000);  // 2 240
        stats.P25.ShouldBe(4000);  // index 2.5 rounds to 3: 4 240
        stats.P50.ShouldBe(6000);  // 6 240
        stats.P75.ShouldBe(9000);  // index 7.5 rounds to 8: 9 240
        stats.P90.ShouldBe(10000); // 10 240
        stats.AtLeast.ShouldBe(SalaryPercentiles.None);
    }

    [Fact]
    public void A_Percentile_On_An_Open_Top_Answer_Is_Only_At_Least()
    {
        var bands = Enumerable.Range(1, 8).Select(i => new SalaryBand(i * 1000, false))
            .Concat([new SalaryBand(15000, true), new SalaryBand(15000, true)])
            .ToList();

        var stats = SalaryMarketAggregator.Stats(bands);

        stats.P90.ShouldBe(15000);
        stats.AtLeast.ShouldBe(SalaryPercentiles.P90);
    }

    [Fact]
    public void A_Slice_Under_The_Threshold_Is_Never_A_Cell()
    {
        // 15 seniors with a known experience range, 14 juniors with none.
        var responses = Enumerable.Range(0, 15).Select(i => Response(100_000 + i * 1000))
            .Concat(Enumerable.Range(0, 14).Select(i => Response(50_000 + i * 1000, SalaryMarketLevel.Junior, experience: null)))
            .ToList();

        var cells = SalaryMarketAggregator.Aggregate(responses, minimum: 15);

        cells.ShouldContain(c => c.Dimension == SalaryMarketDimension.All && c.Stats.Count == 29);
        cells.ShouldContain(c => c.Dimension == SalaryMarketDimension.Level && c.Bucket == "Senior" && c.Stats.Count == 15);
        cells.ShouldNotContain(c => c.Bucket == "Junior");
        cells.ShouldContain(c => c.Dimension == SalaryMarketDimension.Experience && c.Bucket == "SixToTen" && c.Stats.Count == 15);
        cells.ShouldAllBe(c => c.Stats.Count >= 15);
    }

    [Fact]
    public void Every_Source_Of_A_Year_Is_Pooled_Into_One_Cell()
    {
        var responses = Enumerable.Range(0, 10).Select(i => Response(100_000, source: "onceki-yazilimci"))
            .Concat(Enumerable.Range(0, 10).Select(i => Response(100_000, source: "yazilimcimaaslari-org")))
            .ToList();

        var overall = SalaryMarketAggregator.Aggregate(responses, minimum: 15).Single(c => c.Dimension == SalaryMarketDimension.All);

        overall.Stats.Count.ShouldBe(20);
    }

    [Fact]
    public void Years_And_Occupations_Stay_Apart()
    {
        var responses = Enumerable.Range(0, 15).Select(_ => Response(10_000, year: 2025))
            .Concat(Enumerable.Range(0, 15).Select(_ => Response(20_000, year: 2026)))
            .Concat(Enumerable.Range(0, 15).Select(_ => Response(30_000, group: "cto")))
            .ToList();

        var overall = SalaryMarketAggregator.Aggregate(responses, minimum: 15)
            .Where(c => c.Dimension == SalaryMarketDimension.All)
            .ToDictionary(c => (c.Year, c.Group), c => c.Stats.P50);

        overall[(2025, "back-end-developer")].ShouldBe(10_000);
        overall[(2026, "back-end-developer")].ShouldBe(20_000);
        overall[(2026, "cto")].ShouldBe(30_000);
    }
}
