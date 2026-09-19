using AfterApply.Application.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogMediaPathTests
{
    private static readonly Guid Id = Guid.Parse("0199a0a0-0000-7000-8000-000000000001");

    [Fact]
    public void Builds_The_Relative_Path()
    {
        BlogMediaPath.For(Id).ShouldBe("/api/blog/media/0199a0a0-0000-7000-8000-000000000001");
    }

    [Theory]
    [InlineData("/api/blog/media/0199a0a0-0000-7000-8000-000000000001")]
    [InlineData("/api/blog/media/0199A0A0-0000-7000-8000-000000000001/")]
    [InlineData("https://api.ekariyerim.com/api/blog/media/0199a0a0-0000-7000-8000-000000000001")]
    [InlineData("http://localhost:5151/api/blog/media/0199a0a0-0000-7000-8000-000000000001")]
    [InlineData("  https://ekariyerim.com/api/blog/media/0199a0a0-0000-7000-8000-000000000001  ")]
    public void Parses_Our_Own_Paths_Relative_Or_Absolute(string url)
    {
        BlogMediaPath.Parse(url).ShouldBe(Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api/blog/media/")]
    [InlineData("/api/blog/media/not-a-guid")]
    [InlineData("/api/cv-documents/0199a0a0-0000-7000-8000-000000000001/content")]
    [InlineData("https://evil.example/api/blog/media/0199a0a0-0000-7000-8000-000000000001?x=1")]
    [InlineData("javascript:alert(1)//api/blog/media/0199a0a0-0000-7000-8000-000000000001")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Rejects_Anything_Else(string? url)
    {
        BlogMediaPath.Parse(url).ShouldBeNull();
    }

    [Fact]
    public void Lists_Every_Media_Id_A_Stored_Html_Points_At()
    {
        var other = Guid.Parse("0199a0a0-0000-7000-8000-000000000002");
        var html = $"<p>x</p><img src=\"{BlogMediaPath.For(Id)}\" alt=\"\"><figure><img src=\"https://ekariyerim.com{BlogMediaPath.For(other)}\"></figure>" +
                   $"<img src=\"{BlogMediaPath.For(Id)}\"><a href=\"/api/cv-documents/{Id}/content\">not media</a>";

        BlogMediaPath.ReferencedIn(html).ShouldBe([Id, other], ignoreOrder: true);
        BlogMediaPath.ReferencedIn("").ShouldBeEmpty();
        BlogMediaPath.ReferencedIn(null).ShouldBeEmpty();
    }
}
