using AfterApply.Domain.Common;

namespace AfterApply.Domain.Documents;

/// <summary>
/// The ATS-readability report of one stored CV — the same deterministic scan the public,
/// account-free page runs, kept here because this file is the user's own and already held with
/// their consent (growth audit 2026-09-14, finding 03b; decided 2026-09-18). One row per document,
/// overwritten on every re-scan: the report describes the file as it is, and the file does not
/// change, so there is no history to keep — a new version of the CV is a new upload.
///
/// Deleted with the document (cascade), and with the account through it. Never written for the
/// anonymous scan, which keeps its promise of storing nothing but a number.
/// </summary>
public sealed class CvDocumentScan : Entity
{
    public Guid CvDocumentId { get; private set; }

    /// <summary>0-100, duplicated out of the report so the CV list can show it without reading the
    /// whole document JSON for every row.</summary>
    public int Score { get; private set; }

    /// <summary>The full report (categories, findings with their evidence, the document summary and
    /// the extracted-text preview) as JSON — the Application layer's <c>CvDocumentScanReport</c>.
    /// Stored whole rather than normalised: it is only ever read back as one thing.</summary>
    public string ReportJson { get; private set; } = string.Empty;

    public DateTimeOffset ScannedAt { get; private set; }

    private CvDocumentScan()
    {
    }

    public static CvDocumentScan Create(Guid cvDocumentId, int score, string reportJson, DateTimeOffset now)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "A scan score is 0-100.");
        }

        return new CvDocumentScan
        {
            CvDocumentId = cvDocumentId,
            Score = score,
            ReportJson = reportJson,
            ScannedAt = now
        };
    }

    public void Replace(int score, string reportJson, DateTimeOffset now)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "A scan score is 0-100.");
        }

        Score = score;
        ReportJson = reportJson;
        ScannedAt = now;
    }
}
