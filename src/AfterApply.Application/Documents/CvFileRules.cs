using System.Text;
using AfterApply.Domain.Documents;

namespace AfterApply.Application.Documents;

/// <summary>Why an uploaded file was refused. Returned as a code rather than a message so the rules
/// stay unit-testable without a localizer; the service turns it into the caller's language.</summary>
public enum CvFileProblem
{
    UnsupportedExtension,
    Empty,
    TooLarge,
    ContentDoesNotMatchExtension
}

/// <summary>
/// Everything the server decides about an uploaded CV before a byte of it reaches storage. All of
/// it runs server-side on purpose: the extension, the declared Content-Type and the file name all
/// arrive from the client and none of the three is evidence of anything.
/// </summary>
public static class CvFileRules
{
    /// <summary>Longest prefix any check below needs (the OLE2 signature, at 8 bytes). The endpoint
    /// only ever buffers this much before deciding.</summary>
    public const int HeaderLengthBytes = 8;

    /// <summary>Cap on the stored display name. Long enough for a real CV file name, short enough
    /// that it cannot be used to push anything else out of a UI or a log line.</summary>
    private const int MaxFileNameLength = 200;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    /// <summary>Local file header of a ZIP entry — a .docx is an OOXML package, which is a ZIP.
    /// Only this variant is accepted: PK\x05\x06 is an empty archive and PK\x07\x08 a spanned one,
    /// and neither is a document.</summary>
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>OLE2 compound file — the legacy .doc container.</summary>
    private static readonly byte[] Ole2Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>
    /// Reads the format the file name claims to be. Extension only — this is the claim, which
    /// <see cref="HeaderMatchesFormat"/> then has to corroborate.
    /// </summary>
    public static CvFileFormat? FormatFromFileName(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return CvFileFormat.Pdf;
        }

        // .docx before .doc: EndsWith(".doc") is false for "cv.docx", but keeping the more
        // specific test first makes that independent of how the two are ordered.
        if (fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return CvFileFormat.Docx;
        }

        return fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) ? CvFileFormat.Doc : null;
    }

    /// <summary>
    /// Whether the first bytes of the file are actually the container the extension promised.
    /// This is what stops an executable, a script or an HTML page from being stored — and later
    /// handed back — under a .pdf name.
    /// </summary>
    public static bool HeaderMatchesFormat(ReadOnlySpan<byte> header, CvFileFormat format) => format switch
    {
        CvFileFormat.Pdf => header.StartsWith(PdfSignature),
        CvFileFormat.Docx => header.StartsWith(ZipSignature),
        CvFileFormat.Doc => header.StartsWith(Ole2Signature),
        _ => false
    };

    /// <summary>
    /// Checks everything knowable before reading the body: the extension, and the length the client
    /// declared. The declared length is not trusted as the real one — it only lets an obviously
    /// oversized upload be refused without reading it — so the read itself stays bounded too (see
    /// LimitedStream at the call site).
    /// </summary>
    public static CvFileProblem? InspectClaim(string fileName, long declaredLength, long maxFileSizeBytes)
    {
        if (FormatFromFileName(fileName) is null)
        {
            return CvFileProblem.UnsupportedExtension;
        }

        if (declaredLength <= 0)
        {
            return CvFileProblem.Empty;
        }

        return declaredLength > maxFileSizeBytes ? CvFileProblem.TooLarge : null;
    }

    /// <summary>
    /// The name to store and show. Everything that could make a name mean something other than
    /// what it reads as is removed here, because this string is rendered in the web app, in the
    /// Content-Disposition header of a download and in the data export:
    /// <list type="bullet">
    /// <item>any directory part, so a name can never contribute to a path;</item>
    /// <item>control characters, which break header lines and log lines;</item>
    /// <item>Unicode bidirectional overrides, the trick that makes "cv-<c>[U+202E]</c>exe.pdf"
    /// read as a PDF while ending in .exe.</item>
    /// </list>
    /// Never used to address storage regardless — <see cref="CvDocument.BuildStorageObjectName"/>
    /// is built from ids alone.
    /// </summary>
    public static string SanitizeFileName(string fileName, CvFileFormat format)
    {
        // Both separators, on every platform: a Windows client sends backslashes and the server
        // runs on Linux, so relying on Path.GetFileName's platform-specific idea of a separator
        // would let "..\..\cv.pdf" through untouched.
        var lastSeparator = fileName.AsSpan().LastIndexOfAny('/', '\\');
        var baseName = lastSeparator >= 0 ? fileName[(lastSeparator + 1)..] : fileName;

        var builder = new StringBuilder(baseName.Length);
        foreach (var character in baseName)
        {
            if (char.IsControl(character) || IsBidirectionalOverride(character))
            {
                continue;
            }

            builder.Append(character);
        }

        var cleaned = builder.ToString().Trim().TrimStart('.');

        if (cleaned.Length <= MaxFileNameLength)
        {
            return cleaned.Length == 0 ? $"cv{format.Extension()}" : cleaned;
        }

        // Trim the stem, not the tail: the extension is what tells a reader (and their OS)
        // what the file is, so it has to survive the truncation.
        var extension = format.Extension();
        cleaned = string.Concat(cleaned.AsSpan(0, MaxFileNameLength - extension.Length), extension);

        // Everything above can legitimately empty the string ("...", a name that was only control
        // characters). A stored row always has a readable name.
        return cleaned.Length == 0 ? $"cv{format.Extension()}" : cleaned;
    }

    // Compared by code point, not by literal: every character below is invisible, so spelling
    // them out would leave a range in the source that no reviewer can read.
    private static bool IsBidirectionalOverride(char character) => character is
        >= (char)0x202A and <= (char)0x202E or // LRE, RLE, PDF, LRO, RLO
        >= (char)0x2066 and <= (char)0x2069 or // LRI, RLI, FSI, PDI
        (char)0x200E or (char)0x200F;          // LRM, RLM
}
