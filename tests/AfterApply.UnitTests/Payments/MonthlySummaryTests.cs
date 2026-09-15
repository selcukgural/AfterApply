using AfterApply.Application.Payments;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

public sealed class MonthlySummaryTests
{
    // 2026-09-15 12:00 UTC = 15:00 in Istanbul (UTC+3).
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Months_are_listed_newest_first_and_padded_to_the_requested_count()
    {
        var months = MonthlySummary.Compute([], [], Now, 3);

        months.Select(m => m.Month).ShouldBe(["2026-09", "2026-08", "2026-07"]);
        months.ShouldAllBe(m => m.PaidCount == 0 && m.GrossMinor == 0 && m.NetMinor == 0);
    }

    [Fact]
    public void A_sale_belongs_to_the_month_it_was_paid_in_istanbul_time()
    {
        // 31 Aug 22:30 UTC is already 1 Sep 01:30 in Istanbul.
        var paidAt = new DateTimeOffset(2026, 8, 31, 22, 30, 0, TimeSpan.Zero);
        var orders = new[] { new LedgerOrder(paidAt, 29900, null, null, TestMode: false) };

        var months = MonthlySummary.Compute(orders, [], Now, 2);

        months.Single(m => m.Month == "2026-09").GrossMinor.ShouldBe(29900);
        months.Single(m => m.Month == "2026-08").GrossMinor.ShouldBe(0);
    }

    [Fact]
    public void A_refund_counts_in_the_month_it_was_made_and_reduces_net_there()
    {
        var orders = new[] { new LedgerOrder(new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero), 29900, null, null, false) };
        var refunds = new[] { new LedgerRefund(new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero), 29900, false) };

        var months = MonthlySummary.Compute(orders, refunds, Now, 2);

        var august = months.Single(m => m.Month == "2026-08");
        august.GrossMinor.ShouldBe(29900);
        august.NetMinor.ShouldBe(29900);
        var september = months.Single(m => m.Month == "2026-09");
        september.RefundedMinor.ShouldBe(29900);
        september.RefundCount.ShouldBe(1);
        september.NetMinor.ShouldBe(-29900);
    }

    [Fact]
    public void Test_mode_rows_are_left_out_of_every_total()
    {
        var orders = new[]
        {
            new LedgerOrder(Now, 29900, null, null, TestMode: true),
            new LedgerOrder(null, null, Now, null, TestMode: true)
        };
        var refunds = new[] { new LedgerRefund(Now, 100, TestMode: true) };

        var month = MonthlySummary.Compute(orders, refunds, Now, 1).Single();

        month.PaidCount.ShouldBe(0);
        month.FailedCount.ShouldBe(0);
        month.RefundCount.ShouldBe(0);
    }

    [Fact]
    public void Failed_and_closed_orders_are_counted_separately_from_sales()
    {
        var orders = new[]
        {
            new LedgerOrder(null, null, Now, null, false),
            new LedgerOrder(null, null, null, Now, false),
            new LedgerOrder(null, null, null, Now, false)
        };

        var month = MonthlySummary.Compute(orders, [], Now, 1).Single();

        month.FailedCount.ShouldBe(1);
        month.CancelledCount.ShouldBe(2);
        month.PaidCount.ShouldBe(0);
    }

    [Fact]
    public void Rows_outside_the_window_are_ignored()
    {
        var orders = new[] { new LedgerOrder(Now.AddMonths(-6), 29900, null, null, false) };

        MonthlySummary.Compute(orders, [], Now, 2).Sum(m => m.GrossMinor).ShouldBe(0);
    }

    [Fact]
    public void Month_start_is_midnight_in_istanbul_expressed_in_utc()
    {
        MonthlySummary.MonthStart(Now, MonthlySummary.MerchantTimeZone)
            .ShouldBe(new DateTimeOffset(2026, 8, 31, 21, 0, 0, TimeSpan.Zero));
        MonthlySummary.MonthKey(new DateTimeOffset(2026, 8, 31, 21, 0, 0, TimeSpan.Zero)).ShouldBe("2026-09");
        MonthlySummary.MonthKey(new DateTimeOffset(2026, 8, 31, 20, 59, 59, TimeSpan.Zero)).ShouldBe("2026-08");
    }
}
