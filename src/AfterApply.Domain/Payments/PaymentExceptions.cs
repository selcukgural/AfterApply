using AfterApply.Domain.Common;

namespace AfterApply.Domain.Payments;

public sealed class PaymentOrderInvalidTransitionException(PaymentOrderStatus from, string action)
    : DomainException("PAYMENT_ORDER_INVALID_TRANSITION", $"Cannot {action} a payment order in status {from}.");

public sealed class PaymentRefundExceedsTotalException()
    : DomainException("PAYMENT_REFUND_EXCEEDS_TOTAL", "The refund would exceed the amount the customer paid.");

public sealed class PaymentRefundAlreadyRequestedException()
    : DomainException("PAYMENT_REFUND_ALREADY_REQUESTED", "A refund has already been requested for this order.");

public sealed class PaymentRefundNotRefundableException()
    : DomainException("PAYMENT_REFUND_NOT_REFUNDABLE", "Only a paid order can be refunded.");
