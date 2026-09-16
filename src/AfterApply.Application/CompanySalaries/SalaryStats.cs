namespace AfterApply.Application.CompanySalaries;

/// <summary>The one calculation the salary page publishes, kept pure so a unit test can pin it.</summary>
public static class SalaryStats
{
    /// <summary>Null on an empty list. An even count averages the two middle values; the result
    /// is rounded to two places like every amount the feature stores.</summary>
    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2m;
        return decimal.Round(median, 2, MidpointRounding.AwayFromZero);
    }
}
