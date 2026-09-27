using AfterApply.Application.SalaryMarket;
using AfterApply.Infrastructure.SalaryMarket;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.SalaryMarket;

public class SalaryMarketServiceTests
{
    private static readonly SalarySurveyEdition[] Editions =
    [
        new(2025, "onceki-yazilimci", "Önceki Yazılımcı", "https://github.com/oncekiyazilimci/2025", "2025-03", 100, 90),
        new(2026, "onceki-yazilimci", "Önceki Yazılımcı", "https://github.com/oncekiyazilimci/2026", "2026-03", 100, 90),
        new(2024, "onceki-yazilimci", "Önceki Yazılımcı", "https://github.com/oncekiyazilimci/2024", "2024-03", 100, 90)
    ];

    private static SalaryMarketCell Cell(int year, string group, int count, int p50,
        SalaryMarketDimension dimension = SalaryMarketDimension.All, string bucket = "") =>
        new(year, group, dimension, bucket, new SalaryStats(count, p50 - 20, p50 - 10, p50, p50 + 10, p50 + 20));

    private static SalaryMarketService Service(params SalaryMarketCell[] cells) =>
        new(Options.Create(new SalaryMarketOptions { MinimumResponses = 15 }), Editions, cells);

    [Fact]
    public void A_Cell_Under_The_Threshold_Is_Dropped_On_Load_Even_If_The_Seed_Carries_It()
    {
        var service = Service(
            Cell(2026, "back-end-developer", 40, 130_000),
            Cell(2026, "back-end-developer", 14, 60_000, SalaryMarketDimension.Level, "Junior"),
            Cell(2026, "cto", 14, 200_000));

        var occupation = service.GetOccupation("back-end-developer")!;

        occupation.Years.Single().Levels.ShouldBeEmpty();
        service.GetOccupation("cto").ShouldBeNull();
        service.GetOccupations().Occupations.Select(o => o.Slug).ShouldBe(["back-end-developer"]);
    }

    [Fact]
    public void An_Unknown_Slug_Is_Nothing() => Service(Cell(2026, "back-end-developer", 40, 1)).GetOccupation("plumber").ShouldBeNull();

    [Fact]
    public void A_Row_Carries_The_Latest_Year_The_Year_Before_And_The_Trend()
    {
        var service = Service(
            Cell(2024, "back-end-developer", 30, 64_500),
            Cell(2025, "back-end-developer", 30, 102_500),
            Cell(2026, "back-end-developer", 30, 132_500));

        var row = service.GetOccupations().Occupations.Single();

        row.LatestYear.ShouldBe(2026);
        row.Latest.P50.ShouldBe(132_500);
        row.PreviousYearP50.ShouldBe(102_500);
        row.Trend.Select(t => t.Year).ShouldBe([2024, 2025, 2026]);
    }

    [Fact]
    public void No_Previous_Median_When_The_Year_Before_Is_Missing()
    {
        var row = Service(Cell(2024, "cto", 30, 1), Cell(2026, "cto", 30, 2)).GetOccupations().Occupations.Single();

        row.PreviousYearP50.ShouldBeNull();
    }

    [Fact]
    public void An_Occupation_Page_Lists_Its_Years_Slices_And_Only_Its_Years_Surveys()
    {
        var service = Service(
            Cell(2025, "back-end-developer", 30, 100_000),
            Cell(2026, "back-end-developer", 40, 130_000),
            Cell(2026, "back-end-developer", 20, 170_000, SalaryMarketDimension.Level, "Senior"),
            Cell(2026, "back-end-developer", 16, 60_000, SalaryMarketDimension.Level, "Junior"),
            Cell(2026, "back-end-developer", 18, 110_000, SalaryMarketDimension.Experience, "ThreeToFive"));

        var occupation = service.GetOccupation("back-end-developer")!;

        occupation.Years.Select(y => y.Year).ShouldBe([2025, 2026]);
        var latest = occupation.Years[^1];
        latest.Levels.Select(l => l.Level).ShouldBe(["Junior", "Senior"]);
        latest.Experience.Select(e => e.Experience).ShouldBe(["ThreeToFive"]);
        occupation.Editions.Select(e => e.Year).ShouldBe([2025, 2026]);
        occupation.MinimumResponses.ShouldBe(15);
    }

    [Fact]
    public void The_Compiled_In_Seed_Loads()
    {
        var service = new SalaryMarketService(Options.Create(new SalaryMarketOptions()));

        var list = service.GetOccupations();

        list.Occupations.Count.ShouldBe(SalaryMarketGroups.All.Count);
        list.Editions.Count.ShouldBeGreaterThanOrEqualTo(9);
        service.GetOccupation("back-end-developer")!.Years.Count.ShouldBe(9);
    }
}
