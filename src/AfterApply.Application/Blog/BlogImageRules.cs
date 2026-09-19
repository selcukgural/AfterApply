using System.Buffers.Binary;

namespace AfterApply.Application.Blog;

public enum BlogImageFormat
{
    Png,
    Jpeg,
    Gif,
    Webp
}

/// <summary>Why an uploaded image was refused — a code, so the rules stay unit-testable without
/// a localizer; the service turns it into the caller's language.</summary>
public enum BlogImageProblem
{
    Empty,
    TooLarge,
    Unsupported
}

/// <summary>
/// Everything the server decides about an uploaded image before a byte of it reaches storage.
/// Only the file's own leading bytes count: the extension and the declared Content-Type both
/// arrive from the client and neither is evidence of anything. SVG is never accepted — served
/// inline, an SVG is a script host, and these images are served inline by design.
/// </summary>
public static class BlogImageRules
{
    /// <summary>Longest prefix any check below needs: the PNG IHDR's width and height end at
    /// byte 24. The service only ever buffers this much before deciding.</summary>
    public const int HeaderLengthBytes = 24;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    /// <summary>Reads the format off the file's first bytes, or null when it is none of the
    /// four. This is the one check that decides whether the upload is an image at all.</summary>
    public static BlogImageFormat? DetectFormat(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PngSignature))
        {
            return BlogImageFormat.Png;
        }

        if (header.StartsWith(JpegSignature))
        {
            return BlogImageFormat.Jpeg;
        }

        if (header.StartsWith(Gif87Signature) || header.StartsWith(Gif89Signature))
        {
            return BlogImageFormat.Gif;
        }

        if (header.Length >= 12 && header.StartsWith(RiffSignature) && header[8..12].SequenceEqual(WebpSignature))
        {
            return BlogImageFormat.Webp;
        }

        return null;
    }

    /// <summary>Checks the length the client declared, before reading the body. Not trusted as
    /// the real length — it only lets an obviously oversized upload be refused unread; the read
    /// itself stays bounded by the multipart limits.</summary>
    public static BlogImageProblem? InspectClaim(long declaredLength, long maxFileSizeBytes)
    {
        if (declaredLength <= 0)
        {
            return BlogImageProblem.Empty;
        }

        return declaredLength > maxFileSizeBytes ? BlogImageProblem.TooLarge : null;
    }

    public static string ContentType(this BlogImageFormat format) => format switch
    {
        BlogImageFormat.Png => "image/png",
        BlogImageFormat.Jpeg => "image/jpeg",
        BlogImageFormat.Gif => "image/gif",
        BlogImageFormat.Webp => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    public static string Extension(this BlogImageFormat format) => format switch
    {
        BlogImageFormat.Png => ".png",
        BlogImageFormat.Jpeg => ".jpg",
        BlogImageFormat.Gif => ".gif",
        BlogImageFormat.Webp => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    /// <summary>Pixel size when the header carries it at a fixed offset (PNG's IHDR, GIF's logical
    /// screen); null for JPEG and WebP, whose dimensions sit in variable-position chunks that are
    /// not worth parsing for an informational column.</summary>
    public static (int Width, int Height)? ReadDimensions(ReadOnlySpan<byte> header, BlogImageFormat format)
    {
        switch (format)
        {
            case BlogImageFormat.Png when header.Length >= 24:
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
                var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
                return width > 0 && height > 0 ? (width, height) : null;
            }
            case BlogImageFormat.Gif when header.Length >= 10:
            {
                int width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
                int height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
                return width > 0 && height > 0 ? (width, height) : null;
            }
            default:
                return null;
        }
    }
}
