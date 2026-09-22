using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Imports;
using AfterApply.Application.Applications.Validators;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

// Both company-profile URLs arrive from the extension's DOM scrape and are later fetched
// server-side by CompanyEnrichmentService, so the allow-lists here are an SSRF boundary, not
// formatting niceties: without them a compromised client could aim a background fetch anywhere.
public class CreateFromExtensionRequestValidatorTests
{
    private readonly CreateFromExtensionRequestValidator _validator = new();

    private static CreateFromExtensionRequest Request(string? linkedInUrl = null, string? kariyerNetUrl = null,
        string? atsUrl = null) =>
        new("Acme", "Backend Engineer", "https://www.kariyer.net/is-ilani/acme-backend-engineer-123",
            Location: null, Description: null, PublishedAt: null, DescriptionHtml: null,
            CompanyLinkedInUrl: linkedInUrl, CompanyKariyerNetUrl: kariyerNetUrl, CompanyAtsUrl: atsUrl);

    [Fact]
    public void Accepts_A_KariyerNet_Company_Profile_Url()
    {
        _validator.Validate(Request(kariyerNetUrl: "https://www.kariyer.net/firma-profil/acme-1166-1810"))
            .IsValid.ShouldBeTrue();
    }

    [Theory]
    // Another host entirely — the case the allow-list exists for.
    [InlineData("https://evil.example/firma-profil/acme")]
    // A host that merely ends with the right letters.
    [InlineData("https://notkariyer.net/firma-profil/acme")]
    // Plaintext, so a redirect could not be re-checked over TLS.
    [InlineData("http://www.kariyer.net/firma-profil/acme")]
    // An internal address, the classic SSRF target.
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    public void Rejects_A_KariyerNet_Url_On_Any_Other_Host_Or_Scheme(string url)
    {
        var result = _validator.Validate(Request(kariyerNetUrl: url));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateFromExtensionRequest.CompanyKariyerNetUrl));
    }

    [Fact]
    public void Still_Rejects_A_Non_LinkedIn_Company_Url()
    {
        var result = _validator.Validate(Request(linkedInUrl: "https://www.kariyer.net/firma-profil/acme-1-2"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateFromExtensionRequest.CompanyLinkedInUrl));
    }

    [Fact]
    public void Accepts_A_Submission_Carrying_Neither_Profile_Url()
    {
        // The normal shape for a posting whose company anchor did not scrape.
        _validator.Validate(Request()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://job-boards.greenhouse.io/stripe")]
    [InlineData("https://boards.greenhouse.io/stripe")]
    [InlineData("https://jobs.lever.co/acme")]
    [InlineData("https://jobs.ashbyhq.com/acme")]
    [InlineData("https://apply.workable.com/acme")]
    [InlineData("https://jobs.smartrecruiters.com/Acme")]
    public void Accepts_A_Company_Board_On_A_Supported_Ats(string url)
    {
        _validator.Validate(Request(atsUrl: url)).IsValid.ShouldBeTrue();
    }

    [Theory]
    // Another host entirely.
    [InlineData("https://evil.example/stripe")]
    // Hosts that merely end with, or merely contain, the right letters.
    [InlineData("https://notgreenhouse.io/stripe")]
    [InlineData("https://greenhouse.io.evil.example/stripe")]
    // Plaintext, so a redirect could not be re-checked over TLS.
    [InlineData("http://job-boards.greenhouse.io/stripe")]
    // The classic SSRF target, and a scheme that is not a fetch at all.
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    public void Rejects_An_Ats_Url_On_Any_Other_Host_Or_Scheme(string url)
    {
        var result = _validator.Validate(Request(atsUrl: url));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateFromExtensionRequest.CompanyAtsUrl));
    }

    [Fact]
    public void Every_Domain_The_Resolver_Knows_As_An_Ats_Is_Accepted_Here()
    {
        // The validator's allow-list is JobPostingSourceResolver.AtsDomains itself. This asserts
        // the pairing rather than the list's contents: adding a site to the resolver must not
        // silently leave its company board rejected at the door.
        foreach (var domain in JobPostingSourceResolver.AtsDomains)
        {
            _validator.Validate(Request(atsUrl: $"https://jobs.{domain}/acme"))
                .IsValid.ShouldBeTrue($"{domain} should be accepted");
        }
    }
}
