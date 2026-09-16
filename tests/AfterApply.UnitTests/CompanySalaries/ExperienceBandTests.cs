using AfterApply.Domain.CompanySalaries;
using Shouldly;

namespace AfterApply.UnitTests.CompanySalaries;

public class ExperienceBandTests
{
    [Theory]
    [InlineData(0, ExperienceBand.ZeroToOne)]
    [InlineData(1, ExperienceBand.ZeroToOne)]
    [InlineData(2, ExperienceBand.TwoToFour)]
    [InlineData(4, ExperienceBand.TwoToFour)]
    [InlineData(5, ExperienceBand.FiveToNine)]
    [InlineData(9, ExperienceBand.FiveToNine)]
    [InlineData(10, ExperienceBand.TenPlus)]
    [InlineData(50, ExperienceBand.TenPlus)]
    public void Maps_Years_To_The_Published_Bands(int years, ExperienceBand expected)
    {
        ExperienceBands.From(years).ShouldBe(expected);
    }
}
