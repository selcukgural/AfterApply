using AfterApply.Domain.Benchmark;

namespace AfterApply.Application.ResponseRates;

public static class ResponseRatePeriods
{
    /// <summary>Three, six or twelve months; <see cref="BenchmarkPeriod.Longer"/> has no window
    /// and the endpoint refuses it before this is reached.</summary>
    public static int MonthsOf(BenchmarkPeriod period) => period switch
    {
        BenchmarkPeriod.LastThreeMonths => 3,
        BenchmarkPeriod.LastSixMonths => 6,
        BenchmarkPeriod.LastTwelveMonths => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "The response-rate table has no open-ended period.")
    };
}
