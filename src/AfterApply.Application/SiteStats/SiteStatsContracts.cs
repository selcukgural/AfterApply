namespace AfterApply.Application.SiteStats;

/// <summary>
/// The site's own running totals, for the landing page and the about page (growth audit
/// 2026-09-14, finding 11). Each figure is null while it is under <c>SiteStats:MinimumCount</c>:
/// a small number shown as social proof reads as its opposite ("6 people"), so the page shows a
/// counter only once it says something, and says nothing at all while none does. Nothing here
/// identifies anyone — three totals over tables that hold no identity to begin with.
/// </summary>
/// <param name="CvScans">Completed anonymous CV scans (<c>CvScanResults</c>).</param>
/// <param name="BenchmarkAnswers">Benchmark answers submitted (<c>BenchmarkSubmissions</c>).</param>
/// <param name="PublishedReviews">Company reviews approved for the public page.</param>
public sealed record SiteStatsResponse(int? CvScans, int? BenchmarkAnswers, int? PublishedReviews)
{
    /// <summary>True when there is at least one figure to show — the page draws the strip only then.</summary>
    public bool HasAny => CvScans is not null || BenchmarkAnswers is not null || PublishedReviews is not null;
}

public interface ISiteStatsService
{
    Task<SiteStatsResponse> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Pure rule, so the threshold is testable without a database: a count below the floor
/// is withheld, not rounded up and not shown.</summary>
public static class SiteStatsRules
{
    public static int? Visible(int count, int minimumCount) => count >= minimumCount ? count : null;
}
