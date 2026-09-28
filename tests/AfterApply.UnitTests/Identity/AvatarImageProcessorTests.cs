using AfterApply.Infrastructure.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

public class AvatarImageProcessorTests
{
    [Theory]
    [InlineData(0, AvatarImageProblem.Empty)]
    [InlineData(-1, AvatarImageProblem.Empty)]
    [InlineData(5 * 1024 * 1024 + 1, AvatarImageProblem.TooLarge)]
    public void A_Claimed_Length_Outside_The_Limits_Is_Refused_Before_Reading(long declaredLength, AvatarImageProblem expected) =>
        AvatarImageProcessor.InspectClaim(declaredLength, 5 * 1024 * 1024).ShouldBe(expected);

    [Fact]
    public void A_Claimed_Length_Within_The_Limit_Is_Read() =>
        AvatarImageProcessor.InspectClaim(5 * 1024 * 1024, 5 * 1024 * 1024).ShouldBeNull();

    [Theory]
    [InlineData(5000, 5000, true)]      // exactly 25 MP
    [InlineData(5000, 5001, false)]     // one row over the budget
    [InlineData(10_000, 2_500, true)]   // the longest side allowed
    [InlineData(10_001, 1, false)]      // a strip: few pixels, but a side past the cap
    [InlineData(0, 100, false)]
    public void The_Pixel_Budget_Is_Judged_On_The_Header_Dimensions(int width, int height, bool fits) =>
        AvatarImageProcessor.FitsPixelBudget(width, height).ShouldBe(fits);

    [Fact]
    public async Task A_Jpeg_With_A_Gps_Position_Comes_Out_As_A_256_Webp_With_No_Metadata()
    {
        using var source = new Image<Rgba32>(640, 480, new Rgba32(40, 90, 200));
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitudeRef, "N");
        source.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitude, [new Rational(41, 1), new Rational(0, 1), new Rational(0, 1)]);
        source.Metadata.ExifProfile.SetValue(ExifTag.Make, "PhoneMaker");
        var upload = await EncodeAsync(source, new JpegEncoder());

        var stored = await AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None);

        using var result = Image.Load(stored);
        result.Metadata.DecodedImageFormat.ShouldBe(WebpFormat.Instance);
        result.Width.ShouldBe(AvatarImageProcessor.OutputSize);
        result.Height.ShouldBe(AvatarImageProcessor.OutputSize);
        result.Metadata.ExifProfile.ShouldBeNull();
        result.Metadata.XmpProfile.ShouldBeNull();
        // Belt and braces: the GPS reference and the maker never appear in the bytes at all.
        System.Text.Encoding.ASCII.GetString(stored).ShouldNotContain("PhoneMaker");
        System.Text.Encoding.ASCII.GetString(stored).ShouldNotContain("Exif");
    }

    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task Png_And_Webp_Are_Accepted_And_A_Non_Square_Photo_Is_Cropped_Square(string format)
    {
        using var source = new Image<Rgba32>(900, 300, new Rgba32(200, 30, 30));
        var upload = await EncodeAsync(source, format == "png" ? new PngEncoder() : new WebpEncoder());

        var stored = await AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None);

        using var result = Image.Load(stored);
        result.Size.ShouldBe(new Size(AvatarImageProcessor.OutputSize, AvatarImageProcessor.OutputSize));
    }

    [Fact]
    public async Task A_Gif_Is_Not_An_Accepted_Format_Even_Though_It_Is_An_Image()
    {
        using var source = new Image<Rgba32>(64, 64);
        var upload = await EncodeAsync(source, new GifEncoder());

        var rejected = await Should.ThrowAsync<AvatarImageRejectedException>(() =>
            AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None));
        rejected.Problem.ShouldBe(AvatarImageProblem.Unsupported);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("%PDF-1.7")]
    [InlineData("MZ\x90\x00 not an image")]
    public async Task Anything_That_Is_Not_An_Image_Is_Unsupported(string text)
    {
        var upload = new MemoryStream(System.Text.Encoding.Latin1.GetBytes(text));

        var rejected = await Should.ThrowAsync<AvatarImageRejectedException>(() =>
            AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None));
        rejected.Problem.ShouldBe(AvatarImageProblem.Unsupported);
    }

    [Fact]
    public async Task A_Truncated_Png_Is_Unsupported_Rather_Than_A_Crash()
    {
        using var source = new Image<Rgba32>(300, 300, new Rgba32(1, 2, 3));
        var whole = (await EncodeAsync(source, new PngEncoder())).ToArray();
        var upload = new MemoryStream(whole[..40]);

        var rejected = await Should.ThrowAsync<AvatarImageRejectedException>(() =>
            AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None));
        rejected.Problem.ShouldBe(AvatarImageProblem.Unsupported);
    }

    [Fact]
    public async Task A_Header_Past_The_Budget_Is_Refused_Before_Its_Pixels_Are_Decoded()
    {
        // 10,001 × 1 is a few bytes on disk and a handful of pixels, but past the per-side cap —
        // the rule that stops a tiny file from declaring a vast canvas.
        using var source = new Image<Rgba32>(10_001, 1);
        var upload = await EncodeAsync(source, new PngEncoder());

        var rejected = await Should.ThrowAsync<AvatarImageRejectedException>(() =>
            AvatarImageProcessor.ProcessAsync(upload, CancellationToken.None));
        rejected.Problem.ShouldBe(AvatarImageProblem.TooManyPixels);
    }

    private static async Task<MemoryStream> EncodeAsync(Image image, SixLabors.ImageSharp.Formats.IImageEncoder encoder)
    {
        var stream = new MemoryStream();
        await image.SaveAsync(stream, encoder);
        stream.Position = 0;
        return stream;
    }
}
