using AfterApply.Application.Companies;
using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

public class CompanyLogoImageTests
{
    [Fact]
    public void Png_Jpeg_And_WebP_Are_Recognised_From_Their_Bytes()
    {
        CompanyLogoImage.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]).ShouldBe("image/png");
        CompanyLogoImage.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]).ShouldBe("image/jpeg");
        CompanyLogoImage.DetectContentType("RIFF\0\0\0\0WEBPVP8 "u8).ShouldBe("image/webp");
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("GIF89a......")]
    [InlineData("<html><body>not an image</body></html>")]
    [InlineData("RIFF\0\0\0\0WAVEfmt ")]
    [InlineData("")]
    public void Anything_Else_Is_Refused_Whatever_It_Claims_To_Be(string body)
    {
        CompanyLogoImage.DetectContentType(System.Text.Encoding.UTF8.GetBytes(body)).ShouldBeNull();
    }
}

public class LinkedInLogoUrlTests
{
    private static string Page(string content) =>
        $"""<html><head><meta property="og:image" content="{content}"></head></html>""";

    [Fact]
    public void Reads_The_Company_Logo_And_Decodes_Entities()
    {
        var url = LinkedInCompanyProfileParser.ExtractLogoUrl(Page(
            "https://media.licdn.com/dms/image/v2/X/company-logo_200_200/B/0/1/acme_logo?e=1&amp;v=beta"));

        url.ShouldBe("https://media.licdn.com/dms/image/v2/X/company-logo_200_200/B/0/1/acme_logo?e=1&v=beta");
    }

    [Theory]
    // LinkedIn's grey placeholder for a company without a logo.
    [InlineData("https://static.licdn.com/aero-v1/sc/h/cs8pjfgyw96g44ln9r7tct85f")]
    // A cover image, not a logo.
    [InlineData("https://media.licdn.com/dms/image/v2/X/company-background_10000/B/0/1/cover")]
    [InlineData("http://media.licdn.com/dms/image/v2/X/company-logo_200_200/B/0/1/acme_logo")]
    [InlineData("https://media.licdn.com.evil.example/dms/image/v2/X/company-logo_200_200/x")]
    [InlineData("https://evil.example/media.licdn.com/company-logo_200_200/x")]
    [InlineData("javascript:alert(1)")]
    public void Anything_That_Is_Not_A_LinkedIn_Logo_Is_Refused(string content)
    {
        LinkedInCompanyProfileParser.ExtractLogoUrl(Page(content)).ShouldBeNull();
    }

    [Fact]
    public void A_Page_Without_One_Has_None()
    {
        LinkedInCompanyProfileParser.ExtractLogoUrl("<html></html>").ShouldBeNull();
    }
}

public class CompanyLogoEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Blocking_Drops_The_Image_And_Stays_Blocked()
    {
        var logo = CompanyLogo.For(Guid.NewGuid());
        logo.Found([1, 2, 3], "image/png", Now);

        logo.Block(Now.AddDays(1));

        logo.Blocked.ShouldBeTrue();
        logo.Content.ShouldBeNull();
        logo.ContentType.ShouldBeNull();
        logo.CheckedAt.ShouldBe(Now.AddDays(1));
    }
}
