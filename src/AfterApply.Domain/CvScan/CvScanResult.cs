using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;

namespace AfterApply.Domain.CvScan;

/// <summary>
/// One anonymous scan score. This is the entire retention footprint of the CV scan: no file, no
/// text, no identifier, no IP address — a number, what kind of file produced it, and when.
///
/// It exists for two reasons, both of which need aggregates and neither of which needs a person:
/// the result page tells a reader where their score falls among everyone else's, and the stopping
/// condition for the whole surface is stated in completed scans (DEVELOPMENT_PLAN.md, V6). A
/// counter alone would answer the second and not the first.
///
/// <b>There is no UserId</b>, the same deliberate absence as <c>BenchmarkSubmission</c>: the scan
/// is answerable by someone who has never heard of the product, and attaching a row to whoever
/// happened to be signed in would turn an anonymous surface into a profile of somebody's CV
/// quality. This table is therefore outside the cascade-from-Users rule (DECISIONS.md 2026-09-07),
/// and nothing is missing here.
/// </summary>
public sealed class CvScanResult : Entity
{
    /// <summary>0-100, as computed by CvScanScoring. Deterministic, so two scans of the same file
    /// contribute the same number.</summary>
    public int Score { get; private set; }

    /// <summary>PDF or DOCX. Kept because it is the one thing that changes what the checks could
    /// even look at — a distribution mixing the two without saying so would be misleading to
    /// whoever reads it next.</summary>
    public CvFileFormat Format { get; private set; }

    public DateTimeOffset ScannedAt { get; private set; }

    private CvScanResult()
    {
    }

    public static CvScanResult Create(int score, CvFileFormat format, DateTimeOffset scannedAt) =>
        new() { Score = score, Format = format, ScannedAt = scannedAt };
}
