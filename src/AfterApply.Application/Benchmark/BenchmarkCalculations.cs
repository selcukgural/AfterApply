using AfterApply.Application.Analytics;

namespace AfterApply.Application.Benchmark;

/// <summary>Which pool the median being shown was drawn from.</summary>
public enum BenchmarkComparisonScope
{
    /// <summary>Neither the sector nor the whole pool has enough answers yet.</summary>
    None,

    /// <summary>The sector is still short, so the figure is everyone's — and the page says so.
    /// Weaker than a sector median and it must never be presented as one; it is here because
    /// "come back in a month" is a poor answer to the only question the page exists for.</summary>
    Overall,

    /// <summary>The sector cleared the threshold on its own. What the page is actually for.</summary>
    Sector
}

/// <summary>What one person is told back after answering.</summary>
/// <param name="YourRate">Their own replies-over-applications, as a percentage.</param>
/// <param name="SampleSize">How many answers their sector holds, including this one — reported
/// whatever the scope, because it is what says how far off a sector median still is.</param>
/// <param name="TotalSubmissions">Everyone who has answered, across every sector.</param>
/// <param name="Scope">Which pool <paramref name="MedianRate"/> came from.</param>
/// <param name="ComparedAgainstCount">How many answers are behind the median shown, or null when
/// there is none. Equals <paramref name="SampleSize"/> for a sector median and
/// <paramref name="TotalSubmissions"/> for the overall one — spelled out rather than left for the
/// reader to work out, because the copy quotes it.</param>
/// <param name="MedianRate">The median of that pool, or null while nothing can be compared.</param>
/// <param name="ShareBelowYou">Percentage of that pool reporting a lower rate, or null.</param>
public sealed record BenchmarkComparison(
    double YourRate,
    int SampleSize,
    int TotalSubmissions,
    BenchmarkComparisonScope Scope,
    int? ComparedAgainstCount,
    double? MedianRate,
    double? ShareBelowYou);

/// <summary>
/// The arithmetic behind the public benchmark, kept pure so the rule that matters can be tested on
/// its own: <b>below the sample threshold nothing is claimed about a sector.</b>
///
/// That rule is inherited from the company metrics (CompanyIntelligenceOptions.HiddenBelow), and so
/// is the direction it may be moved in — raising a threshold only ever hides more, so it is safe;
/// lowering one is a decision about what may be published, not a tuning knob. The number here is
/// lower than the company ladder's 50 on purpose: that ladder guards a figure printed next to a
/// named employer, and this one guards an anonymous sector median with nobody's name attached.
///
/// The same threshold then gets a second use. A sector short of it falls back to the median across
/// every sector, labelled as exactly that. This is the one place the design bends toward being
/// useful early, and it bends without lowering the bar: the *sector* claim still needs its thirty,
/// and a reader is told in the same breath that the figure is not about their field. What it avoids
/// is a page whose only answer, for its first weeks, is "not yet".
///
/// Two deliberate choices about *what* is compared:
///
/// * <b>Median, not mean.</b> There is no visitor identity behind a submission, so one person can
///   answer more than once and a joke answer can be sent on purpose. A median barely moves under
///   either; a mean is at their mercy. The robustness is the point, not a nicety.
/// * <b>The sector cell ignores the period.</b> A rate is a ratio, so it survives being pooled
///   across periods far better than a count would, and splitting on both axes would starve every
///   cell below the threshold for a long time. The period is still stored, and the page's
///   methodology note carries the caveat it exists for: a short window reads low, because
///   applications sent recently have not had time to be answered — the same distortion already
///   recorded against the company metrics.
/// </summary>
public static class BenchmarkCalculations
{
    /// <summary>
    /// Replies over applications, as a percentage rounded to one decimal — the same scale and
    /// rounding every other rate in this API uses, because it *is* AnalyticsCalculations.CalculateRate
    /// rather than a second definition of the same thing. Emitting a 0-1 fraction here would have
    /// been the natural maths and the wrong contract: web/src/lib/dashboard/format.ts takes 0-100,
    /// so a 0.2 would have rendered as "0.2%".
    ///
    /// Everything downstream — the median, the share below — then works on exactly the numbers the
    /// reader is shown, so "40% report a lower rate" can never contradict the figure beside it.
    /// </summary>
    public static double ReplyRate(int applicationCount, int replyCount) =>
        AnalyticsCalculations.CalculateRate(replyCount, applicationCount);

    /// <param name="cellRates">Every rate in the answerer's sector, theirs included.</param>
    /// <param name="allRates">Every rate on record, theirs included.</param>
    public static BenchmarkComparison Compare(
        double yourRate, IReadOnlyList<double> cellRates, IReadOnlyList<double> allRates,
        int minimumSampleSize)
    {
        var sampleSize = cellRates.Count;
        var totalSubmissions = allRates.Count;

        // The sector first — it is the only pool that answers the question actually asked.
        if (sampleSize >= minimumSampleSize)
        {
            return Against(cellRates, BenchmarkComparisonScope.Sector);
        }

        // Then everyone, if there is enough of everyone. Saying "the median across all fields is
        // 22%" off sixty answers is a claim that stands up; saying it *about software* off four
        // would not, which is why the scope travels with the number.
        if (totalSubmissions >= minimumSampleSize)
        {
            return Against(allRates, BenchmarkComparisonScope.Overall);
        }

        // Their own number and how many have answered so far — never a comparison.
        return new BenchmarkComparison(
            yourRate, sampleSize, totalSubmissions, BenchmarkComparisonScope.None, null, null, null);

        BenchmarkComparison Against(IReadOnlyList<double> pool, BenchmarkComparisonScope scope) =>
            new(yourRate, sampleSize, totalSubmissions, scope, pool.Count,
                Median(pool),
                // Strictly below, so identical answers do not inflate anyone's position. Scaled and
                // rounded like every other rate here.
                AnalyticsCalculations.CalculateRate(pool.Count(rate => rate < yourRate), pool.Count));
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;

        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2;
    }
}
