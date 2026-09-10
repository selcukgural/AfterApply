using AfterApply.Domain.Documents;

namespace AfterApply.Application.CvScan;

/// <summary>
/// Everything the checks are allowed to see about an uploaded CV. It is a plain model on purpose:
/// the extractor (PdfPig / OpenXml, both in Infrastructure) is the only code that touches a file,
/// and every check in <see cref="CvScanChecks"/> is a pure function over this record. That split is
/// what makes the score testable without a fixture file for every case — and it keeps the parsing
/// libraries out of the layer the scoring lives in.
/// </summary>
/// <param name="PageCount">Null for .docx: a Word file has no pagination until something renders
/// it, and inventing a number would put a made-up page count next to a finding that claims to be
/// evidenced. The length check reads <see cref="WordCount"/> instead when this is null.</param>
/// <param name="Text">The whole document as the machine reads it, pages joined by a blank line.
/// Capped while extracting (<c>CvScanOptions.MaxExtractedCharacters</c>) — a CV that runs past the
/// cap is already failing the length check.</param>
/// <param name="HeaderFooterText">Text that lives in a real header/footer part. Only .docx can say
/// this exactly; for a PDF the same question is answered from coordinates, because a PDF has no
/// notion of a header beyond "ink near the top of the page".</param>
/// <param name="TableCount">Tables as the format itself declares them (.docx). Null for PDF, where
/// a table is only ever an inference from where the words sit.</param>
public sealed record ExtractedCv(
    CvFileFormat Format,
    int? PageCount,
    int WordCount,
    string Text,
    bool TextTruncated,
    IReadOnlyList<ExtractedCvPage> Pages,
    string HeaderFooterText,
    int? TableCount,
    IReadOnlyList<ExtractedCvFont> Fonts);

/// <param name="Words">Empty when the page carries no text layer — a scanned image is exactly this
/// case, and it is the single most valuable thing this scan can tell someone.</param>
public sealed record ExtractedCvPage(
    int Number,
    double Width,
    double Height,
    string Text,
    IReadOnlyList<ExtractedCvWord> Words);

/// <summary>One word with the box it occupies, in the format's own coordinate space. The origin is
/// the bottom-left corner (PDF's convention, kept rather than flipped so the numbers match anything
/// a reader checks in another tool), so a larger <paramref name="Top"/> is higher up the page.</summary>
public sealed record ExtractedCvWord(string Text, double Left, double Right, double Bottom, double Top);

/// <param name="GlyphCount">How much of the document is set in this face and size. The formatting
/// check ignores a face used for three characters — a stray bullet or a ligature is not an
/// inconsistency anyone should lose points for.</param>
/// <param name="SampleText">A few words actually set in this face, so a finding about typefaces can
/// point at somewhere in the reader's own CV instead of asserting a number at them.</param>
public sealed record ExtractedCvFont(string Name, double Size, int GlyphCount, int Page, string SampleText);

/// <summary>Why a file could not be read at all. Distinct from a low score: a score means the
/// machine read the CV and found problems, this means there was nothing to read.</summary>
public enum CvExtractionFailure
{
    /// <summary>Encrypted or password-protected. Common enough to deserve its own message.</summary>
    PasswordProtected,

    /// <summary>The container is not what its bytes promised, or it is damaged past lenient parsing.</summary>
    Corrupt,

    /// <summary>More pages than the scan will read (<c>CvScanOptions.MaxPages</c>).</summary>
    TooManyPages,

    /// <summary>Parsing ran past its deadline, or the package expands to far more than it claims.
    /// Both are the same answer to the caller and the same defence: a malformed file is a denial of
    /// service surface on an endpoint that anyone can call without an account.</summary>
    TooExpensive,

    /// <summary>.doc (OLE2). Accepted by the upload rules because it is stored elsewhere in the
    /// product, but there is no managed reader for it here and shelling out to one would hand an
    /// anonymous stranger's file to another process. The caller is told to export a PDF.</summary>
    UnsupportedLegacyFormat
}

/// <summary>Thrown by the extractor; the service turns it into a localized 400.</summary>
public sealed class CvExtractionException(CvExtractionFailure failure)
    : Exception($"CV text extraction failed: {failure}.")
{
    public CvExtractionFailure Failure { get; } = failure;
}

public interface ICvTextExtractor
{
    /// <summary>
    /// Reads a CV into <see cref="ExtractedCv"/>. Never writes the file anywhere: the whole point
    /// of this surface is that a stranger's CV exists in memory for the length of one request.
    /// </summary>
    /// <exception cref="CvExtractionException">Nothing could be read — see
    /// <see cref="CvExtractionFailure"/>.</exception>
    Task<ExtractedCv> ExtractAsync(Stream content, CvFileFormat format, CancellationToken cancellationToken);
}
