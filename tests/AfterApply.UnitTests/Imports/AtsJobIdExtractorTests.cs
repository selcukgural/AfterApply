using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

/// <summary>
/// Every case here has a twin in extension/tests/adapters.test.js — the two derive the same id on
/// opposite sides of the wire, and a divergence would split one posting into two Job rows.
/// </summary>
public class AtsJobIdExtractorTests
{
    [Theory]
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/4512345", "stripe/4512345")]
    [InlineData("https://boards.greenhouse.io/stripe/jobs/4512345", "stripe/4512345")]
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/4512345#app", "stripe/4512345")]
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/4512345?gh_src=abc", "stripe/4512345")]
    [InlineData("https://job-boards.greenhouse.io/stripe", null)]
    [InlineData("https://job-boards.greenhouse.io/stripe/jobs/not-a-number", null)]
    public void Greenhouse_Reads_Board_And_Posting(string url, string? expected) =>
        AtsJobIdExtractor.Greenhouse(url).ShouldBe(expected);

    [Theory]
    [InlineData("https://jobs.lever.co/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f", "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    [InlineData("https://jobs.lever.co/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f/apply", "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    [InlineData("https://jobs.lever.co/acme", null)]
    public void Lever_Reads_Site_And_Posting(string url, string? expected) =>
        AtsJobIdExtractor.Lever(url).ShouldBe(expected);

    [Theory]
    [InlineData("https://jobs.ashbyhq.com/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f", "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    [InlineData("https://jobs.ashbyhq.com/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f/application", "acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f")]
    public void Ashby_Reads_Org_And_Posting(string url, string? expected) =>
        AtsJobIdExtractor.Ashby(url).ShouldBe(expected);

    [Theory]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/NVIDIAExternalCareerSite/job/US-CA-Santa-Clara/Senior-Engineer_JR1234567", "nvidia/JR1234567")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/NVIDIAExternalCareerSite/job/Remote/Staff-Engineer_R-98765", "nvidia/R-98765")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Staff-Engineer_R-98765/apply", "nvidia/R-98765")]
    [InlineData("https://wd3.myworkdaysite.com/en-US/recruiting/acme/ExternalSite/job/Istanbul/Backend-Engineer_R-4242", "acme/R-4242")]
    // A search/listing page carries no requisition id, so there is nothing to dedupe on.
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/NVIDIAExternalCareerSite", null)]
    public void Workday_Reads_Tenant_And_Requisition(string url, string? expected) =>
        AtsJobIdExtractor.Workday(url).ShouldBe(expected);

    [Theory]
    [InlineData("https://apply.workable.com/acme/j/A1B2C3D4E5/", "acme/A1B2C3D4E5")]
    [InlineData("https://apply.workable.com/acme/j/A1B2C3D4E5", "acme/A1B2C3D4E5")]
    [InlineData("https://apply.workable.com/acme/", null)]
    public void Workable_Reads_Company_And_Token(string url, string? expected) =>
        AtsJobIdExtractor.Workable(url).ShouldBe(expected);

    [Theory]
    [InlineData("https://jobs.smartrecruiters.com/Acme/743999123456789-backend-engineer", "Acme/743999123456789")]
    [InlineData("https://careers.smartrecruiters.com/Acme/743999123456789", "Acme/743999123456789")]
    [InlineData("https://jobs.smartrecruiters.com/Acme", null)]
    public void SmartRecruiters_Reads_Company_And_Posting(string url, string? expected) =>
        AtsJobIdExtractor.SmartRecruiters(url).ShouldBe(expected);

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void An_Unparsable_Url_Yields_No_Id(string? url)
    {
        AtsJobIdExtractor.Greenhouse(url).ShouldBeNull();
        AtsJobIdExtractor.Lever(url).ShouldBeNull();
        AtsJobIdExtractor.Ashby(url).ShouldBeNull();
        AtsJobIdExtractor.Workday(url).ShouldBeNull();
        AtsJobIdExtractor.Workable(url).ShouldBeNull();
        AtsJobIdExtractor.SmartRecruiters(url).ShouldBeNull();
    }
}
