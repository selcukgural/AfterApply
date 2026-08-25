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
    public void Extract_Returns_Null_When_No_Id_Present(string? url)
    {
        LinkedInJobIdExtractor.Extract(url).ShouldBeNull();
    }
}
