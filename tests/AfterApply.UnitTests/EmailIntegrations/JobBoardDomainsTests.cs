using AfterApply.Application.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class JobBoardDomainsTests
{
    [Theory]
    [InlineData("linkedin.com")]
    [InlineData("LinkedIn.com")]
    [InlineData("kariyer.net")]
    [InlineData("greenhouse.io")]
    public void IsKnown_Exact_Domain_Match_Returns_True(string domain)
    {
        JobBoardDomains.IsKnown(domain).ShouldBeTrue();
    }

    [Theory]
    [InlineData("boards.greenhouse.io")]
    [InlineData("acme.wd5.myworkdayjobs.com")]
    [InlineData("jobs.lever.co")]
    public void IsKnown_Subdomain_Of_Known_Domain_Returns_True(string domain)
    {
        JobBoardDomains.IsKnown(domain).ShouldBeTrue();
    }

    [Theory]
    [InlineData("acme-unknown-test.com")]
    [InlineData("random-personal-test.com")]
    [InlineData("notlinkedin.com")]
    [InlineData(null)]
    [InlineData("")]
    public void IsKnown_Unrecognized_Domain_Returns_False(string? domain)
    {
        JobBoardDomains.IsKnown(domain).ShouldBeFalse();
    }
}
