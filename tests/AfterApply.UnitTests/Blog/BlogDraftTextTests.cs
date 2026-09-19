using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogDraftTextTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData(" \t\n", "  ", "<p></p>")]
    [InlineData("", "", "<p>&nbsp;</p>")]
    [InlineData("", "", "<h2></h2><ul><li><p> </p></li></ul>")]
    [InlineData("", "", "<p><strong></strong><em> </em></p>")]
    public void Nothing_Written_Is_Empty(string? title, string? excerpt, string? html)
    {
        BlogDraftText.HasAny(title, excerpt, html).ShouldBeFalse();
    }

    [Theory]
    [InlineData("a", "", "")]
    [InlineData("", "a", "")]
    [InlineData("", "", "<p>a</p>")]
    [InlineData("", "", "<p>&amp;</p>")]
    [InlineData("", "", "<p><strong>a</strong></p>")]
    [InlineData("", "", "<h2>Başlık</h2>")]
    public void One_Character_Anywhere_Is_Content(string title, string excerpt, string html)
    {
        BlogDraftText.HasAny(title, excerpt, html).ShouldBeTrue();
    }

    [Fact]
    public void Strips_Tags_And_Decodes_Entities()
    {
        BlogDraftText.TextOf("<p>1 &lt; 2</p>").Trim().ShouldBe("1 < 2");
        BlogDraftText.TextOf("<p>a</p><p>b</p>").ShouldContain("a");
        BlogDraftText.TextOf("<p>a</p><p>b</p>").ShouldContain("b");
        BlogDraftText.TextOf(null).ShouldBe(string.Empty);
    }
}
