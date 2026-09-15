using AfterApply.Application.Payments.Contracts;

namespace AfterApply.Application.Payments;

/// <summary>Refunds, always through our own system (the PayTR refund API), never the merchant
/// panel: the order row is the single record of what was charged and returned, and a full refund
/// takes the Pro period back at the same time.</summary>
public interface IPaymentRefundService
{
    /// <summary>The user asks. Null when the order is not theirs.</summary>
    Task<PaymentOrderResponse?> RequestAsync(Guid userId, Guid orderId, string reason, CancellationToken cancellationToken);

    Task<AdminPaymentOrderResponse?> RejectAsync(Guid adminUserId, Guid orderId, string note, CancellationToken cancellationToken);

    /// <summary>Calls PayTR, records the refund, winds the entitlement back on a full refund,
    /// mails the user. Throws a coded exception with PayTR's message when PayTR refuses.</summary>
    Task<AdminPaymentOrderResponse?> RefundAsync(Guid adminUserId, Guid orderId, long? amountMinor, CancellationToken cancellationToken);

    /// <summary>Records a refund the admin verified in the merchant panel, without calling PayTR.</summary>
    Task<AdminPaymentOrderResponse?> MarkRefundedAsync(Guid adminUserId, Guid orderId, long amountMinor, string referenceNo, CancellationToken cancellationToken);
}
