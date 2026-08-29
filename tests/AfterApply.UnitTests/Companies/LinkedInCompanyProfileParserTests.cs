using AfterApply.Application.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

public class LinkedInCompanyProfileParserTests
{
    // Shapes mirror a real fetch of https://www.linkedin.com/company/microsoft/ (plain HTTP GET,
    // honest bot User-Agent, logged out) — verified manually before writing this parser.
    private const string OverviewHtml = """
        <div class="mb-2 flex" data-test-id="about-us__website">
        <dt class="Ipsum-label t-14 font-sans t-black--light lg:babybear:flex-shrink-0 mamabear:mr-3 babybear:mb-1">
          Website
      </dt>
      <dd class="font-sans px-0.25 text-md text-color-text break-words overflow-hidden">


    <a class="link-no-visited-state hover:no-underline" data-tracking-control-name="about_website" href="https://www.linkedin.com/redir/redirect?url=https%3A%2F%2Fnews%2Emicrosoft%2Ecom%2F&amp;urlhash=sqqa&amp;trk=about_website" target="_blank" rel="noopener">news.microsoft.com</a>
      </dd>
    </div>
    <div class="mb-2 flex" data-test-id="about-us__industry">
    <dt class="t-14 font-sans t-black--light">
          Industry
      </dt>
      <dd class="font-sans px-0.25 text-md text-color-text break-words overflow-hidden">

                  Software Development

      </dd>
    </div>
    """;

    private const string JsonLdHtml = """
        <script type="application/ld+json">
        {"@context":"http://schema.org","@graph":[{"@type":"Organization","name":"Microsoft","address":{"type":"PostalAddress","streetAddress":"1 Microsoft Way","addressLocality":"Redmond","addressRegion":"Washington","postalCode":"98052","addressCountry":"US"},"sameAs":"https://news.microsoft.com/"}]}
        </script>
        """;

    [Fact]
    public void Extracts_Industry_From_Overview_Dt_Dd_Pair()
    {
        LinkedInCompanyProfileParser.ExtractIndustry(OverviewHtml).ShouldBe("Software Development");
    }

    [Fact]
    public void Extracts_Website_By_Unwrapping_LinkedIn_Redirect()
    {
        LinkedInCompanyProfileParser.ExtractWebsite(OverviewHtml).ShouldBe("https://news.microsoft.com/");
    }

    [Fact]
    public void Extracts_Country_Code_From_JsonLd_Address()
    {
        LinkedInCompanyProfileParser.ExtractCountryCode(JsonLdHtml).ShouldBe("US");
    }

    [Fact]
    public void Returns_Null_When_Overview_Field_Missing()
    {
        LinkedInCompanyProfileParser.ExtractIndustry("<html><body>No overview here.</body></html>").ShouldBeNull();
        LinkedInCompanyProfileParser.ExtractWebsite("<html><body>No overview here.</body></html>").ShouldBeNull();
        LinkedInCompanyProfileParser.ExtractCountryCode("<html><body>No overview here.</body></html>").ShouldBeNull();
    }
}
