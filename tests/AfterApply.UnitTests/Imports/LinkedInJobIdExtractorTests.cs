using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class LinkedInJobIdExtractorTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/4449445627/", "4449445627")]
    [InlineData("https://www.linkedin.com/jobs/view/4449445627", "4449445627")]
    [InlineData("https://www.linkedin.com/jobs/view/4449445627/?trk=flagship3_search_srp_jobs", "4449445627")]
    [InlineData("https://tr.linkedin.com/jobs/view/1234567890", "1234567890")]
    // The slug form from LinkedIn's listing links and canonical URLs: the id closes the slug.
    [InlineData("https://tr.linkedin.com/jobs/view/software-developer-net-at-veripark-4460989029?position=1&pageNum=0", "4460989029")]
    [InlineData("https://www.linkedin.com/jobs/view/senior-net-developer-ecom-marketplace-at-path-4463992527", "4463992527")]
    [InlineData("https://www.linkedin.com/jobs/view/junior%E2%80%93mid-level-c%23-net-geli%C5%9Ftirici-4464222112/", "4464222112")]
    public void Extract_Returns_Numeric_Id_From_LinkedIn_Job_Url(string url, string expected)
    {
        LinkedInJobIdExtractor.Extract(url).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com/careers/backend-engineer")]
    [InlineData("https://www.linkedin.com/jobs/collections/recommended/")]
    // A slug that happens to contain digits but does not end in an id.
    [InlineData("https://www.linkedin.com/jobs/view/net-8-developer-at-acme")]
    public void Extract_Returns_Null_When_No_Id_Present(string? url)
    {
        LinkedInJobIdExtractor.Extract(url).ShouldBeNull();
    }
}
