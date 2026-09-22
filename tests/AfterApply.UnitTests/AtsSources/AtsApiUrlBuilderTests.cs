using AfterApply.Application.AtsSources;
using AfterApply.Domain.Common;
using Shouldly;

namespace AfterApply.UnitTests.AtsSources;

/// <summary>
/// This builder is the SSRF boundary for ATS enrichment: it decides the only address the fetch is
/// allowed to request. Half of these cases are therefore about refusal, not construction.
/// </summary>
public class AtsApiUrlBuilderTests
{
    private const string Uuid = "8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f";

    [Theory]
    [InlineData(Source.Greenhouse, "stripe/4512345", "https://boards-api.greenhouse.io/v1/boards/stripe/jobs/4512345?content=true")]
    [InlineData(Source.Lever, "acme/" + Uuid, "https://api.lever.co/v0/postings/acme/" + Uuid + "?mode=json")]
    [InlineData(Source.Ashby, "acme/" + Uuid, "https://api.ashbyhq.com/posting-api/job-board/acme")]
    [InlineData(Source.SmartRecruiters, "Acme/743999123456789", "https://api.smartrecruiters.com/v1/companies/Acme/postings/743999123456789")]
    public void Builds_The_Documented_Public_Endpoint(Source source, string externalId, string expected)
    {
        AtsApiUrlBuilder.Build(source, "https://example.com/ignored", externalId)?.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/China-Shenzhen/Manager_JR2026166",
        "https://nvidia.wd5.myworkdayjobs.com/wday/cxs/nvidia/Site/job/China-Shenzhen/Manager_JR2026166")]
    // No locale segment: the remainder is already the site.
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/Site/job/China-Shenzhen/Manager_JR2026166",
        "https://nvidia.wd5.myworkdayjobs.com/wday/cxs/nvidia/Site/job/China-Shenzhen/Manager_JR2026166")]
    // myworkdaysite.com puts the tenant in the path instead of the host.
    [InlineData("https://wd3.myworkdaysite.com/en-US/recruiting/acme/Site/job/Istanbul/Backend_R-4242",
        "https://wd3.myworkdaysite.com/wday/cxs/acme/Site/job/Istanbul/Backend_R-4242")]
    public void Derives_Workdays_Address_From_The_Page_Url(string jobUrl, string expected)
    {
        AtsApiUrlBuilder.Build(Source.Workday, jobUrl, "nvidia/JR2026166")?.ToString().ShouldBe(expected);
    }

    [Theory]
    // Not a posting page — a listing or the site root has nothing to fetch.
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote")]
    // Off the allow-list entirely, and the same path over plain http.
    [InlineData("https://evil.example/en-US/Site/job/Remote/Engineer_R-1")]
    [InlineData("http://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Engineer_R-1")]
    // A host that merely ends with the right letters.
    [InlineData("https://myworkdayjobs.com.evil.example/en-US/Site/job/Remote/Engineer_R-1")]
    // Traversal and encoded separators in the segment copied from the page URL.
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/../../../admin/Engineer_R-1")]
    [InlineData("https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Engineer%2F..%2Fx_R-1")]
    // The tenant itself has to look like a tenant.
    [InlineData("https://wd3.myworkdaysite.com/en-US/recruiting/../Site/job/Remote/Engineer_R-1")]
    public void Refuses_A_Workday_Url_It_Cannot_Prove(string jobUrl)
    {
        AtsApiUrlBuilder.Build(Source.Workday, jobUrl, "nvidia/JR1").ShouldBeNull();
    }

    [Theory]
    // Wrong shape for that ATS's posting id — a path segment is never built from an id we cannot
    // recognise, however harmless it looks.
    [InlineData(Source.Greenhouse, "stripe/not-a-number")]
    [InlineData(Source.Lever, "acme/12345")]
    [InlineData(Source.SmartRecruiters, "Acme/abc")]
    // Traversal and separators smuggled through either half of the composite id.
    [InlineData(Source.Greenhouse, "../../secret/4512345")]
    [InlineData(Source.Greenhouse, "stripe/4512345/../../admin")]
    [InlineData(Source.Greenhouse, "stripe")]
    [InlineData(Source.Greenhouse, "")]
    // Not an ATS at all.
    [InlineData(Source.LinkedIn, "4449445627")]
    [InlineData(Source.Other, "x/y")]
    public void Refuses_An_Id_It_Cannot_Prove(Source source, string externalId)
    {
        AtsApiUrlBuilder.Build(source, "https://example.com/ignored", externalId).ShouldBeNull();
    }

    [Fact]
    public void Workable_Asks_For_The_Board_With_The_Descriptions_In_It()
    {
        // details=true is the whole point: without it the same endpoint answers with the company
        // and a list of titles and no description, which is why 0.9.0 shipped without Workable
        // support at all (DECISIONS.md 2026-09-22).
        AtsApiUrlBuilder.Build(Source.Workable, "https://apply.workable.com/acme/j/A1B2C3D4E5/", "acme/A1B2C3D4E5")
            !.ToString()
            .ShouldBe("https://apply.workable.com/api/v1/widget/accounts/acme?details=true");
    }

    [Theory]
    [InlineData("acme/short")]
    [InlineData("acme/A1B2C3D")]
    [InlineData("acme/NOTHEXADECIMAL")]
    [InlineData("acme/A1B2C3D4E5/extra")]
    public void Workable_Refuses_A_Shortcode_It_Cannot_Prove(string externalId)
    {
        AtsApiUrlBuilder.Build(Source.Workable, "https://apply.workable.com/acme/j/x/", externalId).ShouldBeNull();
    }

    [Fact]
    public void Every_Address_It_Builds_Is_On_Its_Own_Api_Allow_List()
    {
        // The client re-checks this list on every redirect hop; this asserts the builder cannot
        // hand it a starting point the check would already refuse.
        (Source, string, string)[] cases =
        [
            (Source.Greenhouse, "https://x", "stripe/4512345"),
            (Source.Lever, "https://x", $"acme/{Uuid}"),
            (Source.Ashby, "https://x", $"acme/{Uuid}"),
            (Source.SmartRecruiters, "https://x", "Acme/743999123456789"),
            (Source.Workday, "https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/Remote/Engineer_R-1", "nvidia/R-1")
        ];

        foreach (var (source, jobUrl, externalId) in cases)
        {
            var uri = AtsApiUrlBuilder.Build(source, jobUrl, externalId);

            uri.ShouldNotBeNull();
            AfterApply.Application.Common.HostRules.IsHttpsHost(uri, AtsApiUrlBuilder.ApiDomains)
                .ShouldBeTrue($"{source} built {uri}");
        }
    }
}
