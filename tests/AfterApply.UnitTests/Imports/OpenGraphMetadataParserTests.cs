using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class OpenGraphMetadataParserTests
{
    [Fact]
    public void Extracts_Property_When_Property_Attribute_Comes_First()
    {
        const string html = """<html><head><meta property="og:title" content="Senior Backend Developer"></head></html>""";

        OpenGraphMetadataParser.ExtractProperty(html, "og:title").ShouldBe("Senior Backend Developer");
    }

    [Fact]
    public void Extracts_Property_When_Content_Attribute_Comes_First()
    {
        const string html = """<html><head><meta content="Senior Backend Developer" property="og:title"></head></html>""";

        OpenGraphMetadataParser.ExtractProperty(html, "og:title").ShouldBe("Senior Backend Developer");
    }

    [Fact]
    public void Decodes_Html_Entities_In_Content()
    {
        const string html = """<meta property="og:title" content="R&amp;D Engineer">""";

        OpenGraphMetadataParser.ExtractProperty(html, "og:title").ShouldBe("R&D Engineer");
    }

    [Fact]
    public void Returns_Null_When_Property_Missing()
    {
        const string html = """<html><head><title>Fallback Title</title></head></html>""";

        OpenGraphMetadataParser.ExtractProperty(html, "og:title").ShouldBeNull();
    }

    [Fact]
    public void Extracts_Title_Tag_As_Fallback()
    {
        const string html = "<html><head><title>Job Board - Senior Backend Developer</title></head></html>";

        OpenGraphMetadataParser.ExtractTitleTag(html).ShouldBe("Job Board - Senior Backend Developer");
    }

    [Fact]
    public void Returns_Null_When_No_Title_Tag()
    {
        OpenGraphMetadataParser.ExtractTitleTag("<html><head></head></html>").ShouldBeNull();
    }

    [Fact]
    public void Extracts_Canonical_Link_Href()
    {
        const string html = """
            <html><head><link rel="canonical" href="https://pt.linkedin.com/jobs/view/senior-net-backend-developer-at-luza-tecnologia-4453436290"></head></html>
            """;

        OpenGraphMetadataParser.ExtractLinkHref(html, "canonical")
            .ShouldBe("https://pt.linkedin.com/jobs/view/senior-net-backend-developer-at-luza-tecnologia-4453436290");
    }

    [Fact]
    public void Extracts_Canonical_Link_Href_When_Href_Attribute_Comes_First()
    {
        const string html = """<link href="https://example.com/canonical" rel="canonical">""";

        OpenGraphMetadataParser.ExtractLinkHref(html, "canonical").ShouldBe("https://example.com/canonical");
    }

    [Fact]
    public void Returns_Null_When_No_Matching_Rel()
    {
        const string html = """<link rel="icon" href="https://example.com/favicon.ico">""";

        OpenGraphMetadataParser.ExtractLinkHref(html, "canonical").ShouldBeNull();
    }
}
