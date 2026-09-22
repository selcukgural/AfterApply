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
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/4512345", Source.Greenhouse, "stripe/4512345")]
    [InlineData("https://boards.greenhouse.io/stripe/jobs/4512345", Source.Greenhouse, "stripe/4512345")]
    [InlineData("https://jobs.lever.co/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f", Source.Lever, "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    [InlineData("https://jobs.ashbyhq.com/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f", Source.Ashby, "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Staff-Engineer_R-98765", Source.Workday, "nvidia/R-98765")]
    [InlineData("https://wd3.myworkdaysite.com/en-US/recruiting/acme/Site/job/Istanbul/Backend_R-4242", Source.Workday, "acme/R-4242")]
    [InlineData("https://apply.workable.com/acme/j/A1B2C3D4E5/", Source.Workable, "acme/A1B2C3D4E5")]
    [InlineData("https://jobs.smartrecruiters.com/Acme/743999123456789-backend", Source.SmartRecruiters, "Acme/743999123456789")]
    public void Resolve_Identifies_Known_Sites(string url, Source expectedSource, string expectedExternalId)
    {
        var (source, externalId) = JobPostingSourceResolver.Resolve(url);

        source.ShouldBe(expectedSource);
        externalId.ShouldBe(expectedExternalId);
    }

    [Theory]
    [InlineData("https://example.com/careers/backend-engineer")]
    [InlineData("not a url")]
    // Suffix traps: the host has to *be* the site, not merely end with its name.
    [InlineData("https://notgreenhouse.io/stripe/jobs/4512345")]
    [InlineData("https://greenhouse.io.evil.example/stripe/jobs/4512345")]
    public void Resolve_Falls_Back_To_Other_For_Unknown_Or_Invalid_Urls(string url)
    {
        var (source, externalId) = JobPostingSourceResolver.Resolve(url);

        source.ShouldBe(Source.Other);
        externalId.ShouldBeNull();
    }

    [Fact]
    public void A_Known_Site_With_An_Unreadable_Path_Still_Reports_Its_Source()
    {
        // The generic adapter can capture a company's Greenhouse *board* (no posting id in the
        // URL). That is still provenance worth recording; it just cannot be deduped per site.
        var (source, externalId) = JobPostingSourceResolver.Resolve("https://job-boards.greenhouse.io/stripe");

        source.ShouldBe(Source.Greenhouse);
        externalId.ShouldBeNull();
    }

    [Theory]
    [InlineData(Source.Greenhouse, true)]
    [InlineData(Source.Workday, true)]
    [InlineData(Source.SmartRecruiters, true)]
    [InlineData(Source.LinkedIn, false)]
    [InlineData(Source.KariyerNet, false)]
    [InlineData(Source.Other, false)]
    [InlineData(Source.BrowserExtension, false)]
    public void IsAts_Covers_Exactly_The_Six_Ats_Members(Source source, bool expected)
    {
        JobPostingSourceResolver.IsAts(source).ShouldBe(expected);
    }

    [Fact]
    public void Every_Ats_Domain_Resolves_To_An_Ats_Source()
    {
        // Guards the pairing between the resolver table and the validator allow-list it feeds.
        foreach (var domain in JobPostingSourceResolver.AtsDomains)
        {
            var (source, _) = JobPostingSourceResolver.Resolve($"https://jobs.{domain}/acme");
            JobPostingSourceResolver.IsAts(source).ShouldBeTrue($"{domain} should map to an ATS source");
        }
    }
}
