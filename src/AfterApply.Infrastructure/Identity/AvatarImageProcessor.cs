using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AfterApply.Infrastructure.Identity;

public enum AvatarImageProblem
{
    Empty,
    TooLarge,
    Unsupported,
    TooManyPixels
}

/// <summary>A photo the processor would not take, and why.</summary>
public sealed class AvatarImageRejectedException(AvatarImageProblem problem) : Exception($"Avatar image rejected: {problem}.")
{
    public AvatarImageProblem Problem { get; } = problem;
}

/// <summary>
/// Turns an uploaded photo into the one thing that is ever stored: a 256×256 WebP with no
/// metadata (DECISIONS.md 2026-09-28). The upload itself is never kept — re-encoding is what drops
/// EXIF (a phone photo's GPS position among it), leaves nothing of a file that is something else
/// as well as an image, and keeps every stored photo the same small size.
///
/// Guarded in the order an attacker would try: the declared length first, then the header alone
/// (format and dimensions, read without decoding pixels) against a pixel budget, and only then the
/// decode — so a small file that would inflate to gigabytes of pixels is refused before a byte of
/// it is decompressed. Only the JPEG, PNG and WebP decoders exist in the configuration used here;
/// anything else is an unknown format, not a code path.
/// </summary>
public static class AvatarImageProcessor
{
    public const int OutputSize = 256;

    public const string OutputContentType = "image/webp";

    public const string OutputExtension = "webp";

    /// <summary>25 MP: a 48 MP phone sensor's default JPEG is 12 MP, and the web app sends a cropped
    /// square far below this anyway. Decoded as RGBA that is ~100 MB at worst.</summary>
    public const long MaxPixels = 25_000_000;

    public const int MaxSide = 10_000;

    private static readonly Configuration DecodeConfiguration =
        new(new JpegConfigurationModule(), new PngConfigurationModule(), new WebpConfigurationModule());

    private static readonly DecoderOptions DecodeOptions = new() { Configuration = DecodeConfiguration, MaxFrames = 1 };

    private static readonly WebpEncoder Encoder = new()
    {
        FileFormat = WebpFileFormatType.Lossy,
        Quality = 82,
        SkipMetadata = true
    };

    /// <summary>The problem with a declared length alone, or null when it is worth reading.</summary>
    public static AvatarImageProblem? InspectClaim(long declaredLength, long maxFileSizeBytes) => declaredLength switch
    {
        <= 0 => AvatarImageProblem.Empty,
        _ when declaredLength > maxFileSizeBytes => AvatarImageProblem.TooLarge,
        _ => null
    };

    /// <summary>Whether the dimensions fit the pixel budget.</summary>
    public static bool FitsPixelBudget(int width, int height) =>
        width > 0 && height > 0 && width <= MaxSide && height <= MaxSide && (long)width * height <= MaxPixels;

    /// <summary>Re-encodes <paramref name="content"/> (seekable) to the stored photo.</summary>
    /// <exception cref="AvatarImageRejectedException">Not a JPEG/PNG/WebP, unreadable, or over the pixel budget.</exception>
    public static async Task<byte[]> ProcessAsync(Stream content, CancellationToken cancellationToken)
    {
        ImageInfo info;
        try
        {
            info = await Image.IdentifyAsync(DecodeOptions, content, cancellationToken);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or ImageFormatException or InvalidImageContentException)
        {
            throw new AvatarImageRejectedException(AvatarImageProblem.Unsupported);
        }

        if (!FitsPixelBudget(info.Width, info.Height))
        {
            throw new AvatarImageRejectedException(AvatarImageProblem.TooManyPixels);
        }

        content.Seek(0, SeekOrigin.Begin);

        Image image;
        try
        {
            image = await Image.LoadAsync(DecodeOptions, content, cancellationToken);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or ImageFormatException or InvalidImageContentException)
        {
            throw new AvatarImageRejectedException(AvatarImageProblem.Unsupported);
        }

        using (image)
        {
            // The header is what was budgeted; a decoder that disagrees with it is not trusted either.
            if (!FitsPixelBudget(image.Width, image.Height))
            {
                throw new AvatarImageRejectedException(AvatarImageProblem.TooManyPixels);
            }

            // Orientation first, while the EXIF that says which way is up is still there; then the
            // centre square the web app already cropped to (a square stays as it is), at 256 px.
            image.Mutate(x => x
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(OutputSize, OutputSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

            // SkipMetadata on the encoder already writes none; clearing them as well means no future
            // encoder default can bring the GPS position back.
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;

            await using var output = new MemoryStream();
            await image.SaveAsync(output, Encoder, cancellationToken);
            return output.ToArray();
        }
    }
}
