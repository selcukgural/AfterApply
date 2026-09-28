using AfterApply.Application.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

public class WebsiteIconParserTests
{
    private static readonly Uri Page = new("https://www.acme.com.tr/tr/");

    private static IReadOnlyList<string> Candidates(string head) =>
        WebsiteIconParser.Candidates($"<html><head>{head}</head><body></body></html>", Page).Select(u => u.AbsoluteUri).ToList();

    [Fact]
    public void The_Apple_Touch_Icon_Comes_First_Then_Icons_By_Declared_Size()
    {
        var candidates = Candidates("""
            <link rel="icon" type="image/png" sizes="32x32" href="/favicon-32.png">
            <link rel="icon" type="image/png" sizes="192x192" href="/android-chrome-192.png">
            <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon-180.png">
            """);

        candidates.ShouldBe([
            "https://www.acme.com.tr/apple-touch-icon-180.png",
            "https://www.acme.com.tr/android-chrome-192.png",
            "https://www.acme.com.tr/favicon-32.png",
            "https://www.acme.com.tr/apple-touch-icon.png"
        ]);
    }

    [Fact]
    public void Relative_Links_Resolve_Against_The_Page_And_Entities_Are_Decoded()
    {
        var candidates = Candidates("""<link href='static/logo.png?v=1&amp;x=2' rel='shortcut icon'>""");

        candidates[0].ShouldBe("https://www.acme.com.tr/tr/static/logo.png?v=1&x=2");
    }

    [Theory]
    [InlineData("""<link rel="icon" type="image/svg+xml" href="/icon.svg">""")]
    [InlineData("""<link rel="icon" href="/brand/mark.SVG">""")]
    [InlineData("""<link rel="icon" href="/favicon.ico">""")]
    [InlineData("""<link rel="icon" type="image/x-icon" href="/favicon">""")]
    [InlineData("""<link rel="icon" href="data:image/png;base64,iVBORw0KGgo=">""")]
    [InlineData("""<link rel="icon" href="javascript:alert(1)">""")]
    [InlineData("""<link rel="stylesheet" href="/site.css">""")]
    public void Svg_Ico_Inline_Script_And_Non_Icon_Links_Are_Never_Candidates(string head)
    {
        // Only the conventional location is left.
        Candidates(head).ShouldBe(["https://www.acme.com.tr/apple-touch-icon.png"]);
    }

    [Fact]
    public void At_Most_A_Handful_Is_Tried()
    {
        var links = string.Concat(Enumerable.Range(1, 10).Select(i => $"""<link rel="icon" sizes="{i * 10}x{i * 10}" href="/i{i}.png">"""));

        Candidates(links).Count.ShouldBe(WebsiteIconParser.MaxCandidates);
    }

    [Theory]
    [InlineData("https://acme.com.tr", "https://acme.com.tr/")]
    [InlineData("http://www.acme.com.tr/hakkimizda?x=1", "https://www.acme.com.tr/")]
    [InlineData("  https://Örnek.com.tr/  ", "https://xn--rnek-4qa.com.tr/")]
    public void The_Home_Page_Is_Always_The_Sites_Root_Over_Https(string website, string expected) =>
        WebsiteIconParser.HomePage(website)!.AbsoluteUri.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acme.com.tr")]
    [InlineData("ftp://acme.com.tr/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://localhost/")]
    [InlineData("https://intranet/")]
    [InlineData("https://admin.localhost/")]
    [InlineData("https://user:pass@acme.com.tr/")]
    public void Anything_But_A_Plain_Host_Name_Has_No_Home_Page(string? website) =>
        WebsiteIconParser.HomePage(website).ShouldBeNull();
}
