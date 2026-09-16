using AfterApply.Application.Payments.Contracts;

namespace AfterApply.Application.Payments;

/// <summary>A paid, failed, cancelled or expired order as the monthly ledger sees it.
/// <paramref name="ClosedAt"/> is when a cancelled/expired order was closed.</summary>
public sealed record LedgerOrder(
    DateTimeOffset? PaidAt,
    long? TotalAmountMinor,
    DateTimeOffset? FailedAt,
    DateTimeOffset? ClosedAt,
    bool TestMode);

public sealed record LedgerRefund(DateTimeOffset At, long AmountMinor, bool TestMode);

/// <summary>
/// The admin's month-by-month totals, computed in memory from projected rows so the definitions
/// live in one testable place: a sale belongs to the month it was paid, a refund to the month it
/// was made (even when that is a later month than the sale), a cancellation to the month the
/// order was closed. Months are the merchant's calendar (Europe/Istanbul), not UTC — a payment at
/// 01:30 on the first of the month is that month's revenue, whatever UTC says.
/// </summary>
public static class MonthlySummary
{
    public static readonly TimeZoneInfo MerchantTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static IReadOnlyList<PaymentsMonthSummary> Compute(
        IEnumerable<LedgerOrder> orders,
        IEnumerable<LedgerRefund> refunds,
        DateTimeOffset now,
        int months)
    {
        var buckets = new SortedDictionary<string, Bucket>(StringComparer.Ordinal);
        // Month keys are built in the merchant's calendar, not by adding months to a UTC instant:
        // the UTC start of an Istanbul month is 21:00 of the previous UTC day, and adding a month
        // to that lands in the previous Istanbul month whenever the month lengths differ.
        var local = TimeZoneInfo.ConvertTime(now, MerchantTimeZone);
        var firstMonth = new DateTime(local.Year, local.Month, 1).AddMonths(-(Math.Max(months, 1) - 1));
        for (var i = 0; i < Math.Max(months, 1); i++)
        {
            var month = firstMonth.AddMonths(i);
            buckets[$"{month.Year:0000}-{month.Month:00}"] = new Bucket();
        }

        foreach (var order in orders.Where(o => !o.TestMode))
        {
            if (order.PaidAt is { } paidAt && Get(buckets, paidAt) is { } paidBucket)
            {
                paidBucket.PaidCount++;
                paidBucket.GrossMinor += order.TotalAmountMinor ?? 0;
            }
            else if (order.FailedAt is { } failedAt && Get(buckets, failedAt) is { } failedBucket)
            {
                failedBucket.FailedCount++;
            }
            else if (order.ClosedAt is { } closedAt && Get(buckets, closedAt) is { } closedBucket)
            {
                closedBucket.CancelledCount++;
            }
        }

        foreach (var refund in refunds.Where(r => !r.TestMode))
        {
            if (Get(buckets, refund.At) is { } bucket)
            {
                bucket.RefundCount++;
                bucket.RefundedMinor += refund.AmountMinor;
            }
        }

        return buckets
            .Select(kv => new PaymentsMonthSummary(kv.Key, kv.Value.PaidCount, kv.Value.GrossMinor, kv.Value.RefundedMinor,
                kv.Value.RefundCount, kv.Value.CancelledCount, kv.Value.FailedCount, kv.Value.GrossMinor - kv.Value.RefundedMinor))
            .Reverse()
            .ToList();
    }

    /// <summary>"yyyy-MM" of an instant in the merchant's calendar.</summary>
    public static string MonthKey(DateTimeOffset at)
    {
        var local = TimeZoneInfo.ConvertTime(at, MerchantTimeZone);
        return $"{local.Year:0000}-{local.Month:00}";
    }

    /// <summary>The UTC instant the merchant's month containing <paramref name="at"/> begins.</summary>
    public static DateTimeOffset MonthStart(DateTimeOffset at, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var localStart = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(localStart, zone.GetUtcOffset(localStart)).ToUniversalTime();
    }

    private static Bucket? Get(SortedDictionary<string, Bucket> buckets, DateTimeOffset at) =>
        buckets.TryGetValue(MonthKey(at), out var bucket) ? bucket : null;

    private sealed class Bucket
    {
        public int PaidCount;
        public long GrossMinor;
        public long RefundedMinor;
        public int RefundCount;
        public int CancelledCount;
        public int FailedCount;
    }
}
