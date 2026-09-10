using AfterApply.Domain.Documents;

namespace AfterApply.Application.CvScan.Contracts;

/// <summary>
/// What one scan sends back. Everything in here is derived from the file the caller just uploaded
/// and none of it is kept: the file is never written to disk, the text lives in memory for the
/// length of the request, and the only thing that outlives the response is an anonymous number
/// (see <c>CvScanResult</c>).
/// </summary>
/// <param name="Score">0-100, and exactly the sum of <paramref name="Categories"/>. No model
/// contributes to it.</param>
/// <param name="ExtractedTextPreview">The CV as the machine reads it — the single most convincing
/// thing this page can show, because a person who sees their two-column layout arrive as braided
/// half-sentences needs no further argument.</param>
/// <param name="ReviewStatus">Why the content notes look the way they do — off, not asked for,
/// unavailable, or ready. The page renders a different thing for each, and none of them is an
/// error the reader has to interpret.</param>
/// <param name="ContentNotes">Layer B: what a model said about the writing. <b>Outside the score
/// entirely</b> and badged as such on the page; an empty list with
/// <see cref="CvReviewStatus.Ready"/> means the model found nothing worth saying.</param>
public sealed record CvScanResponse(
    int Score,
    IReadOnlyList<CvScanCategoryScore> Categories,
    IReadOnlyList<CvScanFinding> Findings,
    CvScanDocumentSummary Document,
    string ExtractedTextPreview,
    bool ExtractedTextTruncated,
    CvReviewStatus ReviewStatus,
    IReadOnlyList<CvContentNote> ContentNotes);

/// <param name="PageCount">Null for .docx, which has no pagination of its own; the page then reads
/// <paramref name="WordCount"/> instead rather than showing an invented number.</param>
public sealed record CvScanDocumentSummary(CvFileFormat Format, int? PageCount, int WordCount);

/// <summary>
/// One scan request. Arrives as multipart/form-data, so every field is a form part rather than
/// JSON — the file has to be one, and splitting the rest into a JSON part would give the endpoint
/// two places to look for the same request.
/// </summary>
/// <param name="Locale">Which language to write the content notes in. The deterministic findings
/// are localized in the browser from their codes; the model's prose cannot be, so it has to be
/// asked for in the right language.</param>
/// <param name="ConsentAccepted">Required. A CV can carry special-category personal data (KVKK
/// art. 6) and this endpoint reads it without an account behind it, so explicit consent is checked
/// at the boundary that does the reading rather than only in the UI.</param>
/// <param name="ElapsedMilliseconds">How long the form was open before it was submitted, as the
/// page measured it. Paired with the honeypot and read the same way: it is set by the client, so a
/// determined script can lie about it, and it is here to stop the undetermined ones — the crawler
/// that posts to every form it finds the instant it finds it. The rate limit is what bounds the
/// rest.</param>
/// <param name="ContentNotesRequested">The optional second consent: send the extracted text to the
/// model for content notes. Separate from <paramref name="ConsentAccepted"/> and separately
/// refusable — the scan works without it, and a box that has to be ticked for the feature to work
/// at all is not really optional. Unticked by default, like every consent in this product.</param>
/// <param name="Website">Honeypot, the same one the public benchmark form uses: a field no human
/// ever sees, so anything in it came from something filling every input it found. The usual answer
/// — a CAPTCHA — is a third-party script the CSP forbids and the Cookie Policy denies the site
/// carries.</param>
public sealed record CvScanRequest(
    Stream Content,
    string FileName,
    long DeclaredLength,
    bool ConsentAccepted,
    bool ContentNotesRequested,
    string? Website,
    long? ElapsedMilliseconds,
    string Locale);

public interface ICvScanService
{
    /// <summary>
    /// Reads the CV, scores it and forgets it. Stores nothing but the anonymous score.
    /// </summary>
    /// <exception cref="AfterApply.Application.Documents.CvUploadValidationException">The file was
    /// refused, or consent was missing — carries already-localized messages keyed by form
    /// field.</exception>
    Task<CvScanResponse> ScanAsync(CvScanRequest request, CancellationToken cancellationToken);
}
