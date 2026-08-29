using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class LinkedInJobSlugParserTests
{
    [Fact]
    public void Parses_Title_And_Company_From_Canonical_Slug()
    {
        var (title, company) = LinkedInJobSlugParser.Parse(
            "https://pt.linkedin.com/jobs/view/senior-net-backend-developer-at-luza-tecnologia-4453436290");

        title.ShouldBe("Senior Net Backend Developer");
        company.ShouldBe("Luza Tecnologia");
    }

    [Fact]
    public void Returns_Null_For_Bare_Numeric_Jobs_View_Url()
    {
        var (title, company) = LinkedInJobSlugParser.Parse("https://www.linkedin.com/jobs/view/4453436290/");

        title.ShouldBeNull();
        company.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void Returns_Null_For_Invalid_Input(string? input)
    {
        var (title, company) = LinkedInJobSlugParser.Parse(input);

        title.ShouldBeNull();
        company.ShouldBeNull();
    }

    [Fact]
    public void Returns_Null_When_Slug_Has_No_At_Separator()
    {
        var (title, company) = LinkedInJobSlugParser.Parse("https://www.linkedin.com/jobs/view/senior-backend-developer-4453436290");

        title.ShouldBeNull();
        company.ShouldBeNull();
    }

    [Fact]
    public void Uses_The_Last_At_Separator_When_Title_Word_Coincides()
    {
        var (title, company) = LinkedInJobSlugParser.Parse(
            "https://www.linkedin.com/jobs/view/officer-at-large-at-acme-corp-123456");

        title.ShouldBe("Officer At Large");
        company.ShouldBe("Acme Corp");
    }
}
