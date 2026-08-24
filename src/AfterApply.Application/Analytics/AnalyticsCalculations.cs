namespace AfterApply.Application.Analytics;

public static class AnalyticsCalculations
{
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
