namespace AfterApply.Infrastructure.CvScan;

/// <summary>
/// Sizing and switches for the anonymous CV scan. Every number here bounds work that a stranger
/// can ask for without an account, so all of them are configuration rather than constants: if the
/// endpoint is ever being leaned on, the answer has to be a config change, not a redeploy.
/// </summary>
public sealed class CvScanOptions
{
    public const string SectionName = "CvScan";

    /// <summary>Name of the named HttpClient layer B calls Vertex through. Public because the eval
    /// harness has to re-point exactly this one client at a real socket — the integration suite
    /// blocks outbound HTTP for everything else (see NoOutboundHttpStartup).</summary>
    public const string ReviewHttpClientName = "vertex-cv-review";

    /// <summary>The whole surface. Off means every route in the group answers 404 — the same shape
    /// the email-forwarding and company-intelligence flags use. It is also the stopping condition's
    /// mechanism: DEVELOPMENT_PLAN.md (V6) says the surface closes behind a flag if four weeks pass
    /// without 300 completed scans and a 5% signup conversion.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Layer B — the model's content notes, which sit outside the score. Off by default and off
    /// everywhere until the GCP side is provisioned (see DEPLOYMENT.md): with it off the optional
    /// consent box is not even offered, because an offer that cannot be taken up is noise. Turning
    /// it on never changes the score — that is the split the whole feature rests on.
    /// </summary>
    public bool LlmEnabled { get; init; }

    /// <summary>Layer B's provider settings. Bound from <c>CvScan:Review</c>.</summary>
    public CvReviewSettings Review { get; init; } = new();

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

    /// <summary>
    /// Where the content notes come from and what they are allowed to cost.
    /// </summary>
    public sealed class CvReviewSettings
    {
        /// <summary>The GCP project the model is called in. Empty means unconfigured, and the
        /// provider refuses rather than guessing — a wrong project id would be a silent bill in
        /// somebody else's account.</summary>
        public string ProjectId { get; init; } = string.Empty;

        /// <summary>
        /// The Vertex region, and a privacy decision rather than a latency one: pinned to the EU,
        /// so the text of a CV is processed in the same place the rest of this product's data
        /// already lives and no new country appears in the privacy policy. Never set this to
        /// <c>global</c> — that is precisely the guarantee it would drop.
        /// </summary>
        public string Location { get; init; } = "europe-west1";

        public string Model { get; init; } = "gemini-2.5-flash-lite";

        /// <summary>What the model is allowed to see. Well below the extraction cap: the notes are
        /// about how a CV is written, and the first fifteen thousand characters of one say that as
        /// well as all of it — while bounding the per-request cost.</summary>
        public int MaxInputCharacters { get; init; } = 15_000;

        /// <summary>
        /// Model calls per day, counted from <c>CvScanResults</c> rather than from a provider
        /// dashboard. When it is reached layer B degrades and layer A keeps working, which is the
        /// behaviour the plan asks for: the reader still gets a score, and the page says the notes
        /// are unavailable rather than pretending the CV had nothing worth saying.
        /// </summary>
        public int DailyRequestCeiling { get; init; } = 200;

        /// <summary>A model call that has not answered by now is abandoned and the scan returns
        /// without notes. The reader is waiting on a page for a score that is already computed.</summary>
        public int TimeoutSeconds { get; init; } = 20;
    }
}
