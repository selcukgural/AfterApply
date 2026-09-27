using AfterApply.Application.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

/// <summary>
/// "Paste the posting link into search": the key has to survive what the address bar does to a
/// URL, and must not turn ordinary text searches into URL searches.
/// </summary>
public class JobUrlSearchKeyTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/4012345678/", "linkedin.com/jobs/view/4012345678")]
    [InlineData("https://www.linkedin.com/jobs/view/4012345678/?refId=abc&trackingId=xyz", "linkedin.com/jobs/view/4012345678")]
    [InlineData("https://tr.linkedin.com/jobs/view/senior-developer-at-acme-4012345678", "linkedin.com/jobs/view/4012345678")]
    [InlineData("https://www.linkedin.com/jobs/search/?currentJobId=4012345678&keywords=dotnet", "linkedin.com/jobs/view/4012345678")]
    [InlineData("https://www.linkedin.com/jobs/collections/recommended/?currentJobId=4012345678", "linkedin.com/jobs/view/4012345678")]
    public void LinkedIn_postings_resolve_to_the_canonical_view_path(string pasted, string expected)
    {
        JobUrlSearchKey.TryCreate(pasted, out var key).ShouldBeTrue();
        key.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://www.kariyer.net/is-ilani/acme-yazilim-gelistirici-123456?utm_source=x", "kariyer.net/is-ilani/acme-yazilim-gelistirici-123456")]
    [InlineData("http://boards.greenhouse.io/acme/jobs/42/#apply", "boards.greenhouse.io/acme/jobs/42")]
    [InlineData("  kariyer.net/is-ilani/acme-123456/  ", "kariyer.net/is-ilani/acme-123456")]
    public void Other_sites_drop_query_fragment_www_and_trailing_slash(string pasted, string expected)
    {
        JobUrlSearchKey.TryCreate(pasted, out var key).ShouldBeTrue();
        key.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("backend developer")]
    [InlineData("Acme")]
    [InlineData("C#/.NET")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/jobs/1")]
    public void Text_that_is_not_an_http_address_gets_no_key(string? search)
    {
        JobUrlSearchKey.TryCreate(search, out _).ShouldBeFalse();
    }
}
