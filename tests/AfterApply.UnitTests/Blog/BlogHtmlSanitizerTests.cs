using AfterApply.Application.Blog;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Blog;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

/// <summary>
/// The one control between the editor's markup and the public page's raw-HTML render. Every case
/// here is a vector the page would otherwise execute, or a construct the editor legitimately
/// emits that must survive — both halves matter, because an allowlist that strips too much is
/// "fixed" by someone widening it.
/// </summary>
public class BlogHtmlSanitizerTests
{
    private static readonly BlogHtmlSanitizer Sanitizer = new(Options.Create(new AppOptions { WebBaseUrl = "https://ekariyerim.com" }));
    private static readonly Guid MediaId = Guid.Parse("0199a0a0-0000-7000-8000-000000000001");
    private static readonly string MediaUrl = BlogMediaPath.For(MediaId);

    [Theory]
    [InlineData("<p>hi</p><script>alert(1)</script>", "script")]
    [InlineData("<p onclick=\"alert(1)\">hi</p>", "onclick")]
    [InlineData("<img src=\"/api/blog/media/0199a0a0-0000-7000-8000-000000000001\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<form action=\"https://evil.example\"><button>go</button></form>", "form")]
    [InlineData("<p style=\"width: expression(alert(1))\">x</p>", "expression")]
    [InlineData("<p style=\"background: url(https://evil.example/x.png)\">x</p>", "url(")]
    [InlineData("<object data=\"x.swf\"></object>", "object")]
    [InlineData("<svg onload=\"alert(1)\"></svg>", "svg")]
    [InlineData("<a href=\"https://x.example\" onmouseover=\"alert(1)\">x</a>", "onmouseover")]
    [InlineData("<style>body{display:none}</style><p>x</p>", "style>")]
    public void Strips_Active_Content(string html, string mustNotSurvive)
    {
        Sanitizer.Sanitize(html).ShouldNotContain(mustNotSurvive, Case.Insensitive);
    }

    [Fact]
    public void Keeps_The_Presentational_Styles_The_Editor_Emits()
    {
        const string html = "<p style=\"text-align: center\"><span style=\"color: rgb(220, 38, 38); font-size: 18px; font-family: Georgia, serif\">x</span></p>";

        var result = Sanitizer.Sanitize(html);

        result.ShouldContain("text-align: center");
        // AngleSharp re-serialises colours as rgba; what matters is that the colour survived.
        result.ShouldContain("color: rgba(220, 38, 38, 1)");
        result.ShouldContain("font-size: 18px");
        result.ShouldContain("font-family: Georgia, serif");
    }

    [Fact]
    public void Drops_Css_Properties_Outside_The_Allowlist()
    {
        var result = Sanitizer.Sanitize("<p style=\"color: red; position: fixed; top: 0\">x</p>");

        result.ShouldContain("color: rgba(255, 0, 0, 1)");
        result.ShouldNotContain("position");
        result.ShouldNotContain("top");
    }

    [Fact]
    public void Keeps_Structure_Lists_Tables_And_Task_Items()
    {
        const string html = "<h2>Başlık</h2><ul><li><p>a</p></li></ul><ol start=\"3\"><li><p>b</p></li></ol>" +
                            "<blockquote><p>q</p></blockquote><pre><code>code</code></pre><hr>" +
                            "<table><tbody><tr><th colspan=\"2\">h</th></tr><tr><td>c</td><td>d</td></tr></tbody></table>" +
                            "<ul data-type=\"taskList\"><li data-type=\"taskItem\" data-checked=\"true\"><label><input type=\"checkbox\" checked=\"checked\"><span></span></label><div><p>done</p></div></li></ul>";

        var result = Sanitizer.Sanitize(html);

        foreach (var tag in new[] { "<h2>", "<ul>", "<ol start=\"3\">", "<blockquote>", "<pre>", "<code>", "<hr>", "<table>", "colspan=\"2\"", "<td>" })
        {
            result.ShouldContain(tag);
        }

        result.ShouldContain("data-type=\"taskList\"");
        result.ShouldContain("data-checked=\"true\"");
        result.ShouldContain("type=\"checkbox\"");
        result.ShouldContain("disabled"); // a checkbox on the public page is inert
    }

    [Fact]
    public void A_Non_Checkbox_Input_Is_Removed()
    {
        Sanitizer.Sanitize("<p>x</p><input type=\"text\" name=\"q\">").ShouldNotContain("<input");
    }

    [Fact]
    public void Keeps_Our_Own_Images_And_Folds_An_Absolute_Url_To_The_Relative_Path()
    {
        var html = $"<img src=\"https://api.ekariyerim.com{MediaUrl}\" alt=\"kapak\" width=\"640\">";

        var result = Sanitizer.Sanitize(html);

        result.ShouldContain($"src=\"{MediaUrl}\"");
        result.ShouldContain("alt=\"kapak\"");
        result.ShouldContain("width=\"640\"");
        result.ShouldContain("loading=\"lazy\"");
    }

    [Theory]
    [InlineData("<img src=\"https://evil.example/track.gif\">")]
    [InlineData("<img src=\"data:image/png;base64,iVBORw0KGgo=\">")]
    [InlineData("<img src=\"/api/blog/media/not-a-guid\">")]
    [InlineData("<img src=\"https://api.ekariyerim.com/api/cv-documents/0199a0a0-0000-7000-8000-000000000001/content\">")]
    [InlineData("<img alt=\"no src\">")]
    public void Removes_Any_Image_That_Is_Not_One_Of_Ours(string html)
    {
        Sanitizer.Sanitize($"<p>before</p>{html}<p>after</p>").ShouldNotContain("<img");
    }

    [Fact]
    public void External_Links_Open_In_A_New_Tab_With_Noopener_Noreferrer_Nofollow()
    {
        var result = Sanitizer.Sanitize("<a href=\"https://example.com/x\" target=\"_self\" rel=\"opener\">x</a>");

        result.ShouldContain("href=\"https://example.com/x\"");
        result.ShouldContain("target=\"_blank\"");
        result.ShouldContain("rel=\"noopener noreferrer nofollow\"");
    }

    [Fact]
    public void Internal_Links_Stay_As_They_Are()
    {
        var result = Sanitizer.Sanitize("<a href=\"/tr/guide/x\" target=\"_blank\">x</a><a href=\"#section\">y</a>");

        result.ShouldContain("href=\"/tr/guide/x\"");
        result.ShouldContain("href=\"#section\"");
        result.ShouldNotContain("target=");
        result.ShouldNotContain("rel=");
    }

    [Theory]
    [InlineData("https://ekariyerim.com/tr/guide/kariyer-net-basvurularim-nerede", "/tr/guide/kariyer-net-basvurularim-nerede")]
    [InlineData("https://www.ekariyerim.com/tr/register", "/tr/register")]
    [InlineData("http://EKARIYERIM.com/en/blog/x?utm=1#top", "/en/blog/x?utm=1#top")]
    [InlineData("https://ekariyerim.com", "/")]
    public void Links_To_Our_Own_Site_Written_In_Full_Become_Internal(string href, string expected)
    {
        var result = Sanitizer.Sanitize($"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">x</a>");

        result.ShouldContain($"href=\"{expected}\"");
        result.ShouldNotContain("target=");
        result.ShouldNotContain("rel=");
    }

    [Theory]
    [InlineData("https://ekariyerim.com.evil.example/x")]
    [InlineData("https://notekariyerim.com/x")]
    [InlineData("https://api.ekariyerim.com/x")]
    [InlineData("https://www.kariyer.net/tum-basvurular")]
    public void Look_Alike_And_Other_Hosts_Stay_External(string href)
    {
        var result = Sanitizer.Sanitize($"<a href=\"{href}\">x</a>");

        result.ShouldContain($"href=\"{href}\"");
        result.ShouldContain("rel=\"noopener noreferrer nofollow\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Input_Is_Empty_Output(string html)
    {
        Sanitizer.Sanitize(html).ShouldBe(string.Empty);
    }

    [Fact]
    public void Is_Idempotent()
    {
        var once = Sanitizer.Sanitize($"<h2 style=\"text-align:right\">T</h2><p><a href=\"https://e.com\">l</a> <img src=\"{MediaUrl}\"></p>");

        Sanitizer.Sanitize(once).ShouldBe(once);
    }
}
