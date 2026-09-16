using AfterApply.Application.Payments.Contracts;
using AfterApply.Domain.Payments;

namespace AfterApply.Infrastructure.Payments;

internal static class PaymentOrderMapper
{
    public static PaymentOrderResponse ToUserResponse(this PaymentOrder o) =>
        new(o.Id, o.Plan.ToString(), o.AmountMinor, o.Currency, o.Status.ToString(), o.CreatedAt, o.PaidAt, o.TokenExpiresAt,
            o.FailedReasonCode, o.FailedReasonMsg, o.RefundedAmountMinor, o.RefundRequestedAt, o.EntitlementActiveUntilAfter,
            o.Status is PaymentOrderStatus.Paid or PaymentOrderStatus.PartiallyRefunded && o.RefundableAmountMinor > 0,
            o.TotalAmountMinor);

    public static AdminPaymentOrderResponse ToAdminResponse(this PaymentOrder o, DateTimeOffset now) =>
        new(o.Id, o.UserId, o.Email, o.MerchantOid, o.Plan.ToString(), o.AmountMinor, o.TotalAmountMinor, o.Currency, o.Status.ToString(),
            o.BillingName, o.BillingAddress, o.BillingPhone, o.Locale, o.TermsVersion, o.TermsAcceptedAt, o.PaymentType, o.TestMode,
            o.AmountMismatch, o.FailedReasonCode, o.FailedReasonMsg, o.CreatedAt, o.UpdatedAt, o.TokenExpiresAt, o.PaidAt, o.FailedAt,
            o.CancelledAt, o.CancelledByUserId, o.EntitlementActiveUntilBefore, o.EntitlementActiveUntilAfter, o.RefundRequestedAt,
            o.RefundReason, o.RefundedAt, o.RefundedAmountMinor, o.RefundableAmountMinor, o.RefundReferenceNo, o.RefundedByUserId,
            o.RefundRejectedAt, o.RefundRejectionNote, o.PolicyRefundMinor(now));

    public static PaymentNotificationResponse ToResponse(this PaymentNotification n) =>
        new(n.Id, n.MerchantOid, n.OrderId, n.Status, n.TotalAmountMinor, n.PaymentAmountMinor, n.PaymentType, n.FailedReasonCode,
            n.FailedReasonMsg, n.TestMode, n.HashValid, n.Outcome.ToString(), n.ReceivedAt);
}
