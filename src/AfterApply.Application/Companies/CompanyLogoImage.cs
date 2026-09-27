namespace AfterApply.Application.Companies;

/// <summary>
/// What a stored company logo may be. The type comes from the bytes, never from the remote
/// server's Content-Type: a logo is served back under our own name, so only formats a browser
/// renders as a plain image are kept — PNG, JPEG, WebP. SVG never (it is a document that can carry
/// script), nor anything else. Pure functions — no I/O.
/// </summary>
public static class CompanyLogoImage
{
    /// <summary>Largest logo kept. LinkedIn's 200×200 logos are 5–30 KB; this is room for an odd
    /// one, not a budget to fill.</summary>
    public const int MaxBytes = 256 * 1024;

    public static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
