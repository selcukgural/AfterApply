using AfterApply.Application.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

public class KariyerNetCompanyProfileParserTests
{
    // Shapes copied from a real fetch of
    // https://www.kariyer.net/firma-profil/borusan-lojistik-1166-1810 (plain HTTP GET, honest bot
    // User-Agent, logged out) on 2026-09-06 — the website anchor sits in a row of social icons,
    // each of which looks the same apart from its title.
    private const string WebsiteAnchorHtml = """
        <div class="flex justify-end"><div class="flex gap-2">
        <a href="https://www.linkedin.com/company/somebody" title="Borusan Lojistik LinkedIn" rel="nofollow" target="_blank" class="w-7 h-7 flex"><svg></svg></a>
        <a href="https://www.borusanlojistik.com/tr" title="Borusan Lojistik Web Sitesi" rel="nofollow" target="_blank" class="w-7 h-7 flex items-center justify-center p-1 box-border border border-purple-pastel rounded-full bg-white hover:bg-purple transition-colors duration-300 group"><svg></svg></a>
        </div></div>
        """;

    // The hydration payload is a flat array in which every object field is an *index* into that
    // same array (Nuxt's devalue encoding), not a value — the whole reason ExtractSector chases
    // indexes instead of deserializing. Trimmed to the entries the chase actually walks, with the
    // indexes renumbered to match this shorter array.
    private const string HydrationPayloadHtml = """
        <script type="application/json" id="__NUXT_DATA__">[
        {"id":1,"companySectors":2,"companyInfo":4},
        56971,
        [3],
        {"sectorCode":6,"sectorName":5,"isDefaultSector":7},
        {"workerCount":8,"foundationYear":9},
        "Lojistik",
        "45",
        false,
        "-",
        "1973"
        ]</script>
        """;

    [Fact]
    public void Extracts_The_Website_And_Not_The_Social_Link_Sitting_Next_To_It()
    {
        KariyerNetCompanyProfileParser.ExtractWebsite(WebsiteAnchorHtml).ShouldBe("https://www.borusanlojistik.com/tr");
    }

    [Fact]
    public void Returns_Null_When_The_Profile_Publishes_No_Website()
    {
        // The common case: measured on 2026-09-06, only about 44% of profiles publish one.
        const string html = """<div class="flex gap-2"><a href="https://x.com/someone" title="X" rel="nofollow"></a></div>""";
        KariyerNetCompanyProfileParser.ExtractWebsite(html).ShouldBeNull();
    }

    [Fact]
    public void Refuses_A_Website_That_Is_Not_A_Plain_Web_Url()
    {
        // The value is copied off a third-party page into a column we later render as a link.
        const string html = """<a href="javascript:alert(1)" title="Acme Web Sitesi"></a>""";
        KariyerNetCompanyProfileParser.ExtractWebsite(html).ShouldBeNull();
    }

    [Fact]
    public void Decodes_Html_Entities_In_The_Website_Href()
    {
        const string html = """<a href="https://acme.example/tr?a=1&amp;b=2" title="Acme Web Sitesi"></a>""";
        KariyerNetCompanyProfileParser.ExtractWebsite(html).ShouldBe("https://acme.example/tr?a=1&b=2");
    }

    [Fact]
    public void Extracts_Sector_By_Walking_The_Index_Encoded_Payload()
    {
        KariyerNetCompanyProfileParser.ExtractSector(HydrationPayloadHtml).ShouldBe("Lojistik");
    }

    [Fact]
    public void Returns_Null_Sector_When_There_Is_No_Payload()
    {
        KariyerNetCompanyProfileParser.ExtractSector("<html><body>Nothing here.</body></html>").ShouldBeNull();
    }

    [Fact]
    public void Returns_Null_Sector_When_The_Payload_Is_Truncated_By_The_Fetch_Size_Cap()
    {
        // CompanyEnrichmentService reads a capped number of characters, so a half-payload is a
        // realistic input, not a hypothetical one.
        var truncated = HydrationPayloadHtml[..(HydrationPayloadHtml.Length / 2)];
        KariyerNetCompanyProfileParser.ExtractSector(truncated).ShouldBeNull();
    }

    [Fact]
    public void Returns_Null_Sector_When_An_Index_Points_Outside_The_Payload()
    {
        const string html = """<script type="application/json" id="__NUXT_DATA__">[{"companySectors":99},[1]]</script>""";
        KariyerNetCompanyProfileParser.ExtractSector(html).ShouldBeNull();
    }
}
