using AfterApply.Application.SiteStats;
using Shouldly;

namespace AfterApply.UnitTests.SiteStats;

public class SiteStatsRulesTests
{
    // The whole point of the threshold: a small number is withheld, never rounded, never shown
    // with a caveat. "6 people answered" was the finding this replaces (growth audit 10/11).
    [Theory]
    [InlineData(0, 25, null)]
    [InlineData(6, 25, null)]
    [InlineData(24, 25, null)]
    [InlineData(25, 25, 25)]
    [InlineData(412, 25, 412)]
    public void A_Count_Is_Shown_Only_From_The_Floor_Upwards(int count, int minimum, int? expected)
    {
        SiteStatsRules.Visible(count, minimum).ShouldBe(expected);
    }

    [Fact]
    public void The_Strip_Exists_Only_When_At_Least_One_Figure_Does()
    {
        new SiteStatsResponse(null, null, null).HasAny.ShouldBeFalse();
        new SiteStatsResponse(null, 31, null).HasAny.ShouldBeTrue();
    }
}
