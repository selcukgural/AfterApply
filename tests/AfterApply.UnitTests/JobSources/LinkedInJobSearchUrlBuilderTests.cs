using AfterApply.Application.JobSources;
using AfterApply.Domain.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class LinkedInJobSearchUrlBuilderTests
{
    [Fact]
    public void Builds_The_Verified_Search_Url()
    {
        var uri = LinkedInJobSearchUrlBuilder.Search(".NET Developer", "Türkiye", JobSourceTimeWindow.Week, remoteOnly: false, start: 20);

        uri.AbsoluteUri.ShouldBe("https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search" +
                                "?keywords=.NET%20Developer&location=T%C3%BCrkiye&f_TPR=r604800&start=20");
    }

    [Fact]
    public void Remote_Adds_The_Workplace_Filter_And_Windows_Map_To_Seconds()
    {
        LinkedInJobSearchUrlBuilder.Search("x", "y", JobSourceTimeWindow.Day, true, 0).Query
            .ShouldBe("?keywords=x&location=y&f_TPR=r86400&f_WT=2&start=0");
        LinkedInJobSearchUrlBuilder.Search("x", "y", JobSourceTimeWindow.Month, false, 0).Query
            .ShouldContain("f_TPR=r2592000");
    }

    [Fact]
    public void User_Text_Cannot_Escape_The_Query_String()
    {
        // A profile is user input; whatever it holds ends up as one escaped parameter value.
        var uri = LinkedInJobSearchUrlBuilder.Search("a&b=c#d/e?", "../../admin", JobSourceTimeWindow.Week, false, 0);

        uri.Host.ShouldBe("www.linkedin.com");
        uri.AbsolutePath.ShouldBe("/jobs-guest/jobs/api/seeMoreJobPostings/search");
        uri.Query.ShouldBe("?keywords=a%26b%3Dc%23d%2Fe%3F&location=..%2F..%2Fadmin&f_TPR=r604800&start=0");
    }

    [Fact]
    public void Posting_Url_Takes_Only_A_Numeric_Id()
    {
        LinkedInJobSearchUrlBuilder.Posting("4460989029").AbsoluteUri
            .ShouldBe("https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/4460989029");
        Should.Throw<ArgumentException>(() => LinkedInJobSearchUrlBuilder.Posting("../search"));
        Should.Throw<ArgumentException>(() => LinkedInJobSearchUrlBuilder.Posting(""));
    }
}
