using AfterApply.Domain.Payments;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

public sealed class PaymentOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private static PaymentOrder NewOrder() =>
        PaymentOrder.Create(User, ProPlan.Monthly, 29900, "TL", "ada@example.com", "Ada Lovelace", "Somewhere 1", "+90 555 000 00 00", "tr", "2026-09", Now);

    private static PaymentOrder PaidOrder()
    {
        var order = NewOrder();
        order.AttachToken("tok", Now.AddMinutes(30), Now);
        order.MarkPaid(29900, "card", testMode: false, Now.AddMinutes(5));
        order.RecordEntitlementChange(Now.AddMinutes(5), Now.AddMinutes(5).AddMonths(1), Now.AddMinutes(5));
        return order;
    }

    [Fact]
    public void Merchant_oid_is_the_id_without_hyphens_and_alphanumeric()
    {
        var order = NewOrder();

        order.MerchantOid.ShouldBe(order.Id.ToString("N"));
        order.MerchantOid.Length.ShouldBe(32);
        order.MerchantOid.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c));
        order.Status.ShouldBe(PaymentOrderStatus.Pending);
        order.TermsAcceptedAt.ShouldBe(Now);
    }

    [Fact]
    public void A_zero_amount_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PaymentOrder.Create(User, ProPlan.Monthly, 0, "TL", "a@b.c", "n", "a", "p", "tr", "v", Now));
    }

    [Fact]
    public void Paying_a_pending_order_records_the_charge_and_is_not_late()
    {
        var order = NewOrder();

        var late = order.MarkPaid(29900, "card", testMode: true, Now.AddMinutes(1));

        late.ShouldBeFalse();
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.IsPaid.ShouldBeTrue();
        order.TotalAmountMinor.ShouldBe(29900);
        order.AmountMismatch.ShouldBeFalse();
        order.TestMode.ShouldBeTrue();
        order.PaidAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void A_different_total_is_accepted_but_flagged()
    {
        var order = NewOrder();

        order.MarkPaid(31000, "card", false, Now);

        order.AmountMismatch.ShouldBeTrue();
        order.PaidAmountMinor.ShouldBe(31000);
    }

    [Theory]
    [InlineData(PaymentOrderStatus.Expired)]
    [InlineData(PaymentOrderStatus.Cancelled)]
    [InlineData(PaymentOrderStatus.Failed)]
    public void Success_after_the_order_was_closed_is_honoured_and_reported_as_late(PaymentOrderStatus closedAs)
    {
        var order = NewOrder();
        switch (closedAs)
        {
            case PaymentOrderStatus.Expired: order.MarkExpired(Now); break;
            case PaymentOrderStatus.Cancelled: order.Cancel(null, Now); break;
            case PaymentOrderStatus.Failed: order.MarkFailed(6, "left", Now); break;
        }

        var late = order.MarkPaid(29900, "card", false, Now.AddMinutes(2));

        late.ShouldBeTrue();
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.FailedReasonCode.ShouldBeNull();
    }

    [Fact]
    public void Paying_twice_is_an_invalid_transition_so_the_caller_treats_it_as_a_duplicate()
    {
        var order = PaidOrder();

        Should.Throw<PaymentOrderInvalidTransitionException>(() => order.MarkPaid(29900, "card", false, Now));
        Should.Throw<PaymentOrderInvalidTransitionException>(() => order.MarkFailed(1, "x", Now));
        Should.Throw<PaymentOrderInvalidTransitionException>(() => order.MarkExpired(Now));
        Should.Throw<PaymentOrderInvalidTransitionException>(() => order.Cancel(null, Now));
    }

    [Fact]
    public void Failure_keeps_the_provider_reason()
    {
        var order = NewOrder();

        order.MarkFailed(0, "Kartın limiti yetersiz", Now);

        order.Status.ShouldBe(PaymentOrderStatus.Failed);
        order.FailedReasonCode.ShouldBe(0);
        order.FailedReasonMsg.ShouldBe("Kartın limiti yetersiz");
        order.IsPaid.ShouldBeFalse();
    }

    [Fact]
    public void Provider_rejection_is_a_failure_with_the_reserved_code()
    {
        var order = NewOrder();

        order.MarkProviderRejected("Zorunlu alan degeri gecersiz: user_ip", Now);

        order.Status.ShouldBe(PaymentOrderStatus.Failed);
        order.FailedReasonCode.ShouldBe(PaymentOrder.ProviderRejectedReasonCode);
    }

    [Fact]
    public void Cancelling_records_who_did_it()
    {
        var order = NewOrder();

        order.Cancel(Admin, Now);

        order.Status.ShouldBe(PaymentOrderStatus.Cancelled);
        order.CancelledByUserId.ShouldBe(Admin);
        order.CancelledAt.ShouldBe(Now);
        Should.Throw<PaymentOrderInvalidTransitionException>(() => order.Cancel(null, Now));
    }

    [Fact]
    public void Entitlement_extension_is_the_period_the_order_added()
    {
        var order = PaidOrder();

        order.EntitlementExtension.ShouldBe(Now.AddMinutes(5).AddMonths(1) - Now.AddMinutes(5));
        NewOrder().EntitlementExtension.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Refund_request_moves_to_requested_and_cannot_be_repeated()
    {
        var order = PaidOrder();

        order.RequestRefund("changed my mind", Now);

        order.Status.ShouldBe(PaymentOrderStatus.RefundRequested);
        order.RefundReason.ShouldBe("changed my mind");
        Should.Throw<PaymentRefundAlreadyRequestedException>(() => order.RequestRefund("again", Now));
    }

    [Fact]
    public void Refund_request_is_refused_for_an_unpaid_order()
    {
        Should.Throw<PaymentRefundNotRefundableException>(() => NewOrder().RequestRefund("x", Now));
    }

    [Fact]
    public void Rejecting_a_request_returns_to_paid_and_keeps_the_note()
    {
        var order = PaidOrder();
        order.RequestRefund("x", Now);

        order.RejectRefund("outside the window", Now);

        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.RefundRejectionNote.ShouldBe("outside the window");
        order.RefundRejectedAt.ShouldBe(Now);
    }

    [Fact]
    public void Partial_refunds_accumulate_and_a_full_one_closes_the_order()
    {
        var order = PaidOrder();

        order.RecordRefund(10000, "ref1", Admin, Now);
        order.Status.ShouldBe(PaymentOrderStatus.PartiallyRefunded);
        order.RefundableAmountMinor.ShouldBe(19900);

        order.RequestRefund("rest please", Now);
        order.RecordRefund(19900, "ref2", Admin, Now);

        order.Status.ShouldBe(PaymentOrderStatus.Refunded);
        order.RefundedAmountMinor.ShouldBe(29900);
        order.RefundableAmountMinor.ShouldBe(0);
        order.RefundReferenceNo.ShouldBe("ref2");
        order.RefundedByUserId.ShouldBe(Admin);
    }

    [Fact]
    public void A_refund_beyond_what_was_paid_is_refused()
    {
        var order = PaidOrder();

        Should.Throw<PaymentRefundExceedsTotalException>(() => order.RecordRefund(29901, "r", Admin, Now));
        Should.Throw<PaymentRefundExceedsTotalException>(() => order.RecordRefund(0, "r", Admin, Now));
        order.RecordRefund(29900, "r", Admin, Now);
        Should.Throw<PaymentRefundExceedsTotalException>(() => order.RecordRefund(1, "r", Admin, Now));
    }

    [Fact]
    public void Refund_cap_follows_the_charged_total_not_the_asked_amount()
    {
        var order = NewOrder();
        order.MarkPaid(31000, "card", false, Now);

        order.RefundableAmountMinor.ShouldBe(31000);
    }

    [Fact]
    public void Rejecting_after_a_partial_refund_returns_to_partially_refunded()
    {
        var order = PaidOrder();
        order.RecordRefund(100, "r", Admin, Now);
        order.RequestRefund("more", Now);

        order.RejectRefund("no", Now);

        order.Status.ShouldBe(PaymentOrderStatus.PartiallyRefunded);
    }
}

public sealed class ProPlanPeriodTests
{
    [Fact]
    public void Monthly_adds_a_calendar_month_and_yearly_a_calendar_year()
    {
        var from = new DateTimeOffset(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);

        ProPlanPeriod.Extend(from, ProPlan.Monthly).ShouldBe(new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero));
        ProPlanPeriod.Extend(from, ProPlan.Yearly).ShouldBe(new DateTimeOffset(2027, 1, 31, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_running_entitlement_is_extended_from_its_end_a_lapsed_one_from_now()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

        ProPlanPeriod.NextStart(now.AddDays(10), now).ShouldBe(now.AddDays(10));
        ProPlanPeriod.NextStart(now.AddDays(-10), now).ShouldBe(now);
        ProPlanPeriod.NextStart(null, now).ShouldBe(now);
    }
}
