using AfterApply.Infrastructure.Persistence;
using Shouldly;

namespace AfterApply.UnitTests.Persistence;

public class LikePatternTests
{
    [Theory]
    [InlineData("acme", "%acme%")]
    [InlineData("50%", @"%50\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\x", @"%c:\\x%")]
    public void Contains_Takes_Every_Character_Literally(string text, string expected)
    {
        LikePattern.Contains(text).ShouldBe(expected);
    }

    [Fact]
    public void StartsWith_Anchors_The_Escaped_Text()
    {
        LikePattern.StartsWith("_x").ShouldBe(@"\_x%");
    }

    [Fact]
    public void A_Backslash_Is_Escaped_Before_The_Wildcards_So_It_Cannot_Swallow_Their_Escape()
    {
        LikePattern.Escape(@"\%").ShouldBe(@"\\\%");
    }
}
