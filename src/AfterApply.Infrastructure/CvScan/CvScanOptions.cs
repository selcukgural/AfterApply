namespace AfterApply.Infrastructure.CvScan;

/// <summary>
/// Sizing and switches for the anonymous CV scan. Every number here bounds work that a stranger
/// can ask for without an account, so all of them are configuration rather than constants: if the
/// endpoint is ever being leaned on, the answer has to be a config change, not a redeploy.
/// </summary>
public sealed class CvScanOptions
{
    public const string SectionName = "CvScan";

    /// <summary>The whole surface. Off means every route in the group answers 404 — the same shape
    /// the email-forwarding and company-intelligence flags use. It is also the stopping condition's
    /// mechanism: DEVELOPMENT_PLAN.md (V6) says the surface closes behind a flag if four weeks pass
    /// without 300 completed scans and a 5% signup conversion.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Layer B — the model's content notes, which sit outside the score. Off, and nothing in this
    /// batch reads it: the deterministic layer is the whole product for now, and this exists so
    /// that turning the model on later is a config decision rather than a deploy that also changes
    /// what the page can say. When it is on and a daily ceiling is hit, layer B degrades and layer
    /// A keeps working.
    /// </summary>
    public bool LlmEnabled { get; init; }

    /// <summary>Pages read per file. A CV past this is not being scanned, it is being used as a
    /// parser workload — and the length check has already said everything worth saying about a
    /// ten-page CV.</summary>
    public int MaxPages { get; init; } = 10;

    /// <summary>Characters kept from a document. Enough for a very long CV; a hard stop on a file
    /// whose text expands far past what a CV could hold.</summary>
    public int MaxExtractedCharacters { get; init; } = 30_000;

    /// <summary>How much of the extracted text the response echoes back so the reader can see what
    /// the machine reads. Shorter than the extraction cap because it is a demonstration, not a
    /// copy of their document.</summary>
    public int PreviewCharacters { get; init; } = 4_000;

    /// <summary>
    /// Deadline for parsing one file. A malformed or deliberately pathological PDF is a real denial
    /// of service surface on an anonymous endpoint — this is the ceiling on what one request can
    /// cost, checked between pages.
    /// </summary>
    public int ParseTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Ceiling on what a .docx package may expand to. A 5 MB zip can declare gigabytes of
    /// uncompressed content; the sizes are read from the archive's own directory before a byte is
    /// decompressed, so the refusal costs nothing.
    /// </summary>
    public long MaxUncompressedBytes { get; init; } = 60 * 1024 * 1024;
}
