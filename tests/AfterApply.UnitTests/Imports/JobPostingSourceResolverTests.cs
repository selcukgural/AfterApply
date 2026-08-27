using AfterApply.Application.Imports;
using AfterApply.Domain.Common;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class JobPostingSourceResolverTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/4449445627/", Source.LinkedIn, "4449445627")]
    [InlineData("https://tr.linkedin.com/jobs/view/1234567890", Source.LinkedIn, "1234567890")]
    [InlineData("https://www.kariyer.net/is-ilani/acme-backend-engineer-4539310", Source.KariyerNet, "4539310")]
    public void Resolve_Identifies_Known_Sites(string url, Source expectedSource, string expectedExternalId)
    {
        var (source, externalId) = JobPostingSourceResolver.Resolve(url);

        source.ShouldBe(expectedSource);
        externalId.ShouldBe(expectedExternalId);
    }

    [Theory]
    [InlineData("https://example.com/careers/backend-engineer")]
    [InlineData("not a url")]
    public void Resolve_Falls_Back_To_Other_For_Unknown_Or_Invalid_Urls(string url)
    {
        var (source, externalId) = JobPostingSourceResolver.Resolve(url);

        source.ShouldBe(Source.Other);
        externalId.ShouldBeNull();
    }
}
