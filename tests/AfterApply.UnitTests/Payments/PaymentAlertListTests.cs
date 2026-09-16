using AfterApply.Application.Payments;
using AfterApply.Domain.Payments;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

public sealed class PaymentAlertListTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static PaymentNotification Row(string oid, string status, PaymentNotificationOutcome outcome, int minutesAfterT0) =>
        PaymentNotification.Create(oid, null, status, 100, 100, "card", null, null, testMode: false, hashValid: true, outcome, "", T0.AddMinutes(minutesAfterT0));

    [Fact]
    public void The_alert_set_is_what_went_wrong_and_nothing_that_went_right()
    {
        PaymentAlertList.Outcomes.ShouldBe(
            [PaymentNotificationOutcome.BadHash, PaymentNotificationOutcome.UnknownOrder, PaymentNotificationOutcome.LateApplied,
             PaymentNotificationOutcome.Malformed, PaymentNotificationOutcome.Error], ignoreOrder: true);
        PaymentAlertList.Outcomes.ShouldNotContain(PaymentNotificationOutcome.Applied);
        PaymentAlertList.Outcomes.ShouldNotContain(PaymentNotificationOutcome.Duplicate);
        PaymentAlertList.Outcomes.ShouldNotContain(PaymentNotificationOutcome.RefundRecorded);
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData(0, 1)]
    [InlineData(7, 7)]
    [InlineData(9999, 365)]
    public void Days_default_to_thirty_and_stay_inside_a_year(int? requested, int expected) =>
        PaymentAlertList.ClampDays(requested).ShouldBe(expected);

    [Theory]
    [InlineData(null, 10)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    [InlineData(500, 50)]
    public void Page_size_defaults_to_ten_and_caps_at_fifty(int? requested, int expected) =>
        PaymentAlertList.ClampPageSize(requested).ShouldBe(expected);

    [Theory]
    [InlineData(null, null, PaymentAlertSortKey.ReceivedAt, true)]
    [InlineData("receivedAt", "asc", PaymentAlertSortKey.ReceivedAt, false)]
    [InlineData("OUTCOME", null, PaymentAlertSortKey.Outcome, false)]
    [InlineData("status", "DESC", PaymentAlertSortKey.Status, true)]
    [InlineData("merchantOid", "sideways", PaymentAlertSortKey.MerchantOid, false)]
    [InlineData("nonsense", "asc", PaymentAlertSortKey.ReceivedAt, false)]
    [InlineData("nonsense", null, PaymentAlertSortKey.ReceivedAt, true)]
    public void Sort_parses_leniently_with_newest_first_as_the_fallback(string? sort, string? dir, PaymentAlertSortKey key, bool descending) =>
        PaymentAlertList.ParseSort(sort, dir).ShouldBe((key, descending));

    [Fact]
    public void Newest_first_is_the_default_order()
    {
        var rows = new[] { Row("a", "success", PaymentNotificationOutcome.BadHash, 0), Row("b", "success", PaymentNotificationOutcome.BadHash, 2), Row("c", "success", PaymentNotificationOutcome.BadHash, 1) };

        PaymentAlertList.Sort(rows.AsQueryable(), PaymentAlertSortKey.ReceivedAt, descending: true).Select(n => n.MerchantOid).ShouldBe(["b", "c", "a"]);
        PaymentAlertList.Sort(rows.AsQueryable(), PaymentAlertSortKey.ReceivedAt, descending: false).Select(n => n.MerchantOid).ShouldBe(["a", "c", "b"]);
    }

    [Fact]
    public void A_text_sort_groups_by_the_key_and_keeps_newest_first_inside_a_group()
    {
        var rows = new[]
        {
            Row("old-bad", "success", PaymentNotificationOutcome.BadHash, 0),
            Row("unknown", "failed", PaymentNotificationOutcome.UnknownOrder, 1),
            Row("new-bad", "success", PaymentNotificationOutcome.BadHash, 2),
            Row("error", "success", PaymentNotificationOutcome.Error, 3),
        };

        PaymentAlertList.Sort(rows.AsQueryable(), PaymentAlertSortKey.Outcome, descending: false).Select(n => n.MerchantOid)
            .ShouldBe(["new-bad", "old-bad", "error", "unknown"]);
        PaymentAlertList.Sort(rows.AsQueryable(), PaymentAlertSortKey.Status, descending: true).Select(n => n.MerchantOid)
            .ShouldBe(["error", "new-bad", "old-bad", "unknown"]);
        PaymentAlertList.Sort(rows.AsQueryable(), PaymentAlertSortKey.MerchantOid, descending: false).Select(n => n.MerchantOid)
            .ShouldBe(["error", "new-bad", "old-bad", "unknown"]);
    }
}
