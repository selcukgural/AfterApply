using System.Text.RegularExpressions;
using AfterApply.Application.SalaryMarket;
using AfterApply.Infrastructure.Occupations;
using AfterApply.Infrastructure.SalaryMarket;
using Shouldly;

namespace AfterApply.UnitTests.SalaryMarket;

/// <summary>The compiled-in seed, as the tool wrote it: what every environment will publish.</summary>
public class SalaryMarketSeedTests
{
    private static readonly IReadOnlyList<SalaryMarketCell> Cells = SalaryMarketSeed.ReadCells();
    private static readonly IReadOnlyList<SalarySurveyEdition> Editions = SalaryMarketSeed.ReadEditions();

    [Fact]
    public void No_Cell_Is_Under_The_Published_Threshold() =>
        Cells.ShouldAllBe(c => c.Stats.Count >= new SalaryMarketOptions().MinimumResponses);

    [Fact]
    public void Every_Cell_Belongs_To_A_Published_Occupation() =>
        Cells.ShouldAllBe(c => SalaryMarketGroups.BySlug(c.Group) != null);

    [Fact]
    public void Percentiles_Are_In_Order() =>
        Cells.ShouldAllBe(c => c.Stats.P10 <= c.Stats.P25 && c.Stats.P25 <= c.Stats.P50
                                                          && c.Stats.P50 <= c.Stats.P75 && c.Stats.P75 <= c.Stats.P90);

    [Fact]
    public void Every_Year_With_Figures_Names_Its_Surveys()
    {
        var years = Cells.Select(c => c.Year).Distinct().ToList();

        years.ShouldAllBe(year => Editions.Any(e => e.Year == year));
        Editions.ShouldAllBe(e => e.Used <= e.Responses && e.SourceUrl.StartsWith("https://"));
    }

    [Fact]
    public void Nine_Years_Of_The_First_Source_Are_In()
    {
        Editions.Where(e => e.SourceCode == "onceki-yazilimci").Select(e => e.Year).ShouldBe(Enumerable.Range(2018, 9));
        Cells.ShouldContain(c => c.Year == 2026 && c.Group == "back-end-developer" && c.Dimension == SalaryMarketDimension.All);
    }

    [Fact]
    public void Written_Cells_Read_Back_Unchanged()
    {
        var sample = Cells.Where(c => c.Stats.AtLeast != SalaryPercentiles.None).Take(3)
            .Concat(Cells.Where(c => c.Stats.AtLeast == SalaryPercentiles.None).Take(3))
            .ToList();
        using var text = new StringWriter();
        SalaryMarketSeed.WriteCells(text, sample);

        var read = SalaryMarketSeed.ReadCells(new StringReader(text.ToString()));

        read.ShouldBe(sample);
        text.ToString().ShouldNotContain("\r");
    }

    [Fact]
    public void Written_Editions_Read_Back_Unchanged()
    {
        using var text = new StringWriter();
        SalaryMarketSeed.WriteEditions(text, Editions);

        SalaryMarketSeed.ReadEditions(new StringReader(text.ToString())).ShouldBe(Editions);
    }
}

public class SalaryMarketGroupsTests
{
    [Fact]
    public void Slugs_Are_Unique_Lower_Case_Addresses()
    {
        SalaryMarketGroups.All.Select(g => g.Slug).ShouldBeUnique();
        SalaryMarketGroups.All.ShouldAllBe(g => Regex.IsMatch(g.Slug, "^[a-z0-9]+(-[a-z0-9]+)*$"));
    }

    [Fact]
    public void Linked_Catalogue_Codes_Exist_And_Belong_To_One_Occupation()
    {
        var catalogue = OccupationSeedReader.Read(1).Select(r => r.Code).ToHashSet();
        var codes = SalaryMarketGroups.All.SelectMany(g => g.OccupationCodes).ToList();

        codes.ShouldAllBe(code => catalogue.Contains(code));
        codes.ShouldBeUnique();
    }
}
