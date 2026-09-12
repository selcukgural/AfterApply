using AfterApply.Application.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class LinkedInJobPostingParserTests
{
    [Fact]
    public void Flattens_The_Description_To_Plain_Text_With_Paragraphs()
    {
        var detail = LinkedInJobPostingParser.Parse(LinkedInJobSourceFixtures.Posting(LinkedInJobSourceFixtures.RichDescription));

        detail.Description.ShouldNotBeNull();
        detail.Description.ShouldNotContain("<");
        detail.Description.ShouldStartWith("We enable financial institutions to become digital leaders.");
        detail.Description.ShouldContain("best clients for great & exciting projects.");
        detail.Description.ShouldContain(".NET 8, C#\nPostgreSQL");
        detail.Description.ShouldEndWith("Apply now!");
        detail.Description.ShouldNotContain("Show more");
        detail.Description.ShouldNotContain("\n\n\n");
    }

    [Fact]
    public void Reads_The_Four_Criteria_By_Position()
    {
        var detail = LinkedInJobPostingParser.Parse(LinkedInJobSourceFixtures.Posting("<p>x</p>",
            seniority: "Entry level", employmentType: "Contract", jobFunction: "Finance", industries: "Banking and Insurance"));

        detail.Seniority.ShouldBe("Entry level");
        detail.EmploymentType.ShouldBe("Contract");
        detail.JobFunction.ShouldBe("Finance");
        detail.Industries.ShouldBe("Banking and Insurance");
    }

    [Fact]
    public void Missing_Sections_Are_Null_Not_Errors()
    {
        var detail = LinkedInJobPostingParser.Parse("<html><body>nothing here</body></html>");

        detail.Description.ShouldBeNull();
        detail.Seniority.ShouldBeNull();
        detail.Industries.ShouldBeNull();
    }
}
