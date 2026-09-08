using AfterApply.Domain.Benchmark;

namespace AfterApply.Application.Benchmark.Contracts;

/// <summary>
/// One answer to the public benchmark form. Nullable throughout so a missing field fails
/// validation with a message rather than silently defaulting to zero or to the first enum member —
/// "0 applications" and "did not answer" must not look the same.
/// </summary>
/// <param name="Website">Honeypot. A real form never shows this field, so anything in it came from
/// something filling every input it found. It is here because the usual answer — reCAPTCHA,
/// hCaptcha, Turnstile — is a third-party script, which the CSP forbids and the Cookie Policy
/// denies the site carries; the same constraint that shaped the visit counter shapes this.</param>
public sealed record SubmitBenchmarkRequest(
    int? ApplicationCount,
    int? ReplyCount,
    BenchmarkSector? Sector,
    BenchmarkPeriod? Period,
    BenchmarkSeniority? Seniority,
    BenchmarkLocation? Location,
    string? Locale,
    string? Website);

/// <param name="Sector">Echoed back so the page can name the cell it is comparing against.</param>
/// <param name="MinimumSampleSize">Carried on the result, not just on the summary, so a withheld
/// comparison can say how far off it is without a second request — and so the response explains
/// its own silence rather than leaving a reader to guess it is a bug.</param>
/// <param name="Scope">Which pool the median came from: the answerer's own sector, everyone, or
/// nothing yet. The page renders a different sentence for each — an overall median presented as a
/// sector one would be the single most misleading thing this feature could do.</param>
public sealed record BenchmarkResultResponse(
    BenchmarkSector Sector,
    /// <summary>The two counts the rate came from, echoed back. The caller obviously knows them —
    /// they just typed them — but a result that carries its own inputs renders without the form's
    /// state, and a rate shown with nothing behind it invites "did I mistype that?".</summary>
    int ApplicationCount,
    int ReplyCount,
    double YourRate,
    int SampleSize,
    int TotalSubmissions,
    int MinimumSampleSize,
    BenchmarkComparisonScope Scope,
    int? ComparedAgainstCount,
    double? MedianRate,
    double? ShareBelowYou);

public sealed record BenchmarkSectorCount(BenchmarkSector Sector, int Count);

/// <summary>
/// What the page can show before anyone has answered anything — how many have taken part, and how
/// far each sector still is from having enough to compare against.
/// </summary>
/// <param name="MinimumSampleSize">Published rather than hidden: a reader deciding whether to
/// believe the number is entitled to know the bar it had to clear.</param>
public sealed record BenchmarkSummaryResponse(
    int TotalSubmissions,
    int MinimumSampleSize,
    IReadOnlyList<BenchmarkSectorCount> BySector);
