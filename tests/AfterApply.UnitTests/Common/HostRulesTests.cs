using AfterApply.Application.Common;
using Shouldly;

namespace AfterApply.UnitTests.Common;

public class HostRulesTests
{
    [Theory]
    [InlineData("https://linkedin.com/x", "linkedin.com", true)]
    [InlineData("https://www.linkedin.com/x", "linkedin.com", true)]
    [InlineData("https://tr.linkedin.com/x", "linkedin.com", true)]
    // The two shapes a naive Contains/StartsWith check lets through.
    [InlineData("https://notlinkedin.com/x", "linkedin.com", false)]
    [InlineData("https://linkedin.com.evil.example/x", "linkedin.com", false)]
    [InlineData("https://evil.example/linkedin.com", "linkedin.com", false)]
    public void IsHost_Matches_The_Domain_And_Its_Subdomains_Only(string url, string domain, bool expected)
    {
        HostRules.IsHost(new Uri(url), domain).ShouldBe(expected);
    }

    [Fact]
    public void IsHost_Ignores_Case()
    {
        HostRules.IsHost(new Uri("https://WWW.LinkedIn.COM/x"), "linkedin.com").ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://jobs.lever.co/acme/x", true)]
    // Plain http is refused outright rather than upgraded — see HostRules.
    [InlineData("http://jobs.lever.co/acme/x", false)]
    [InlineData("https://jobs.example.com/acme/x", false)]
    public void IsHttpsHost_Requires_Https_And_An_Allowed_Domain(string url, bool expected)
    {
        HostRules.IsHttpsHost(new Uri(url), "greenhouse.io", "lever.co").ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://boards.greenhouse.io/stripe", true)]
    [InlineData("http://boards.greenhouse.io/stripe", false)]
    [InlineData("https://notgreenhouse.io/stripe", false)]
    [InlineData("https://greenhouse.io.evil.example/stripe", false)]
    [InlineData("https://169.254.169.254/latest/meta-data/", false)]
    [InlineData("/relative/path", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsHttpsUrlOnAllowedHost_Refuses_Anything_It_Cannot_Prove(string? url, bool expected)
    {
        HostRules.IsHttpsUrlOnAllowedHost(url, "greenhouse.io").ShouldBe(expected);
    }
}
