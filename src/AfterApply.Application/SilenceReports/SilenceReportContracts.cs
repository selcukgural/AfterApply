using AfterApply.Application.Common;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.SilenceReports;

namespace AfterApply.Application.SilenceReports;

/// <summary>
/// One anonymous report from a company page. Nullable throughout so a missing field fails
/// validation with a message rather than defaulting to the first enum member.
/// </summary>
/// <param name="PromiseGiven">Optional: null = "not said".</param>
/// <param name="Website">Honeypot, as on the benchmark form: the page never shows it, so anything
/// in it came from something filling every input it found.</param>
/// <param name="Source">The channel from the link's <c>utm_source</c>, mapped by the page to the
/// benchmark's list. Optional and last.</param>
public sealed record SubmitSilenceReportRequest(
    SilenceStage? Stage,
    SilenceWait? Wait,
    bool? PromiseGiven,
    string? Locale,
    string? Website,
    BenchmarkSource? Source = null);

/// <summary>How many reports of one stage are in the window.</summary>
public sealed record SilenceStageCount(SilenceStage Stage, int Count);

/// <summary>
/// A company's reports over the window, present only once they clear the publication floor —
/// below it the whole block is null and nothing says how far off it is, the same rule as every
/// other company-level figure.
/// </summary>
public sealed record CompanySilenceReports(int Count, IReadOnlyList<SilenceStageCount> ByStage);

/// <summary>The floor the page's method line states.</summary>
public sealed record SilenceReportThresholds(int MinimumReports, int MinimumQuarters, int WindowMonths);

public interface ISilenceReportService
{
    /// <summary>Stores the report for the company behind <paramref name="slug"/>. False when no
    /// company has that slug. <paramref name="requesterKey"/> is the caller's connection address,
    /// used only to derive the Redis repeat-block key and never stored.</summary>
    /// <exception cref="SilenceReportRecentException">The same requester reported this company
    /// within the repeat window.</exception>
    Task<bool> SubmitAsync(string slug, SubmitSilenceReportRequest request, string requesterKey,
        CancellationToken cancellationToken);
}

/// <summary>This company was reported from here recently. Deliberately says no more — not when,
/// not how many.</summary>
public sealed class SilenceReportRecentException()
    : CodedException("SILENCE_REPORT_RECENT", "This company was already reported from this connection recently.");
