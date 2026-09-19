using AfterApply.Application.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogImageRulesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0x02, 0x80, 0, 0, 0x01, 0xE0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00];
    private static readonly byte[] Gif = [.. "GIF89a"u8.ToArray(), 0x20, 0x03, 0x58, 0x02, 0xF7, 0x00, 0x00];
    private static readonly byte[] Webp = [.. "RIFF"u8.ToArray(), 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray(), .. "VP8 "u8.ToArray()];

    [Fact]
    public void Detects_The_Four_Formats_By_Their_Bytes()
    {
        BlogImageRules.DetectFormat(Png).ShouldBe(BlogImageFormat.Png);
        BlogImageRules.DetectFormat(Jpeg).ShouldBe(BlogImageFormat.Jpeg);
        BlogImageRules.DetectFormat(Gif).ShouldBe(BlogImageFormat.Gif);
        BlogImageRules.DetectFormat(Webp).ShouldBe(BlogImageFormat.Webp);
    }

    [Theory]
    [InlineData("MZ\x90\x00")]                 // a Windows executable renamed .png
    [InlineData("%PDF-1.7")]                   // a document
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\">")] // a script host
    [InlineData("<html><script>")]             // markup
    [InlineData("RIFF\x24\x00\x00\x00WAVE")]   // RIFF, but not WebP
    [InlineData("")]
    public void Refuses_Anything_Else(string leadingText)
    {
        var header = System.Text.Encoding.Latin1.GetBytes(leadingText);

        BlogImageRules.DetectFormat(header).ShouldBeNull();
    }

    [Fact]
    public void The_Header_Buffer_Covers_The_Png_Dimensions()
    {
        BlogImageRules.HeaderLengthBytes.ShouldBeGreaterThanOrEqualTo(24);
    }

    [Fact]
    public void Reads_Png_And_Gif_Dimensions_From_The_Header()
    {
        BlogImageRules.ReadDimensions(Png, BlogImageFormat.Png).ShouldBe((640, 480));
        BlogImageRules.ReadDimensions(Gif, BlogImageFormat.Gif).ShouldBe((800, 600));
        BlogImageRules.ReadDimensions(Jpeg, BlogImageFormat.Jpeg).ShouldBeNull();
        BlogImageRules.ReadDimensions(Webp, BlogImageFormat.Webp).ShouldBeNull();
    }

    [Fact]
    public void A_Truncated_Png_Header_Yields_No_Dimensions_Rather_Than_Garbage()
    {
        BlogImageRules.ReadDimensions(Png.AsSpan(0, 16), BlogImageFormat.Png).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, BlogImageProblem.Empty)]
    [InlineData(-1, BlogImageProblem.Empty)]
    [InlineData(5 * 1024 * 1024 + 1, BlogImageProblem.TooLarge)]
    public void Inspects_The_Declared_Length(long declared, BlogImageProblem expected)
    {
        BlogImageRules.InspectClaim(declared, 5 * 1024 * 1024).ShouldBe(expected);
    }

    [Fact]
    public void A_Length_Within_The_Cap_Passes()
    {
        BlogImageRules.InspectClaim(5 * 1024 * 1024, 5 * 1024 * 1024).ShouldBeNull();
    }

    [Fact]
    public void Content_Types_And_Extensions_Are_The_Canonical_Ones()
    {
        BlogImageFormat.Png.ContentType().ShouldBe("image/png");
        BlogImageFormat.Jpeg.ContentType().ShouldBe("image/jpeg");
        BlogImageFormat.Gif.ContentType().ShouldBe("image/gif");
        BlogImageFormat.Webp.ContentType().ShouldBe("image/webp");
        BlogImageFormat.Jpeg.Extension().ShouldBe(".jpg");
    }
}
