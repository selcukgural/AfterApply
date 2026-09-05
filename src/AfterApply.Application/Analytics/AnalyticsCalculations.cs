using AfterApply.Application.Analytics.Contracts;

namespace AfterApply.Application.Analytics;

public static class AnalyticsCalculations
{
    /// <summary>
    /// Buckets application dates into <paramref name="weeks"/> consecutive Monday-start UTC weeks,
    /// the last of which contains <paramref name="now"/>. Empty weeks are emitted with a zero count
    /// so the trend keeps a constant x-scale, and anything outside the window is dropped.
    /// </summary>
    public static IReadOnlyList<ApplicationsPerWeekItem> BuildWeeklyBuckets(
        IEnumerable<DateTimeOffset> appliedAt,
        DateTimeOffset now,
        int weeks)
    {
        if (weeks <= 0)
        {
            return [];
        }

        var firstWeekStart = StartOfWeek(now).AddDays(-7 * (weeks - 1));
        var counts = new int[weeks];

        foreach (var applied in appliedAt)
        {
            var index = (StartOfWeek(applied).DayNumber - firstWeekStart.DayNumber) / 7;
            if (index >= 0 && index < weeks)
            {
                counts[index]++;
            }
        }

        return [.. Enumerable.Range(0, weeks)
            .Select(i => new ApplicationsPerWeekItem(firstWeekStart.AddDays(7 * i), counts[i]))];
    }

    private static DateOnly StartOfWeek(DateTimeOffset value)
    {
        var date = DateOnly.FromDateTime(value.UtcDateTime);
        // DayOfWeek counts from Sunday==0; shift so Monday==0 to get an ISO-style week start.
        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    public static double CalculateRate(int count, int total) =>
        total <= 0 ? 0 : Math.Round(100.0 * count / total, 1);

    public static double? Average(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : Math.Round(values.Average(), 1);

    public static double? Median(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        var median = sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];

        return Math.Round(median, 1);
    }
}
