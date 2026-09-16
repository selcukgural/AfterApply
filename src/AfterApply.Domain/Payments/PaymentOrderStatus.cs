namespace AfterApply.Domain.Payments;

/// <summary>
/// Lifecycle of a PayTR order. <see cref="Pending"/> is the only state before money moves; the
/// three ways out of it are the provider's notification (<see cref="Paid"/>/<see cref="Failed"/>),
/// the payment window closing with no notification (<see cref="Expired"/>), or the user or an
/// admin closing it (<see cref="Cancelled"/>). A notification of success is honoured from any of
/// those three late states too — if PayTR says the card was charged, it was. Refunds only ever
/// start from a paid order and go through our own admin panel (never the PayTR merchant panel), so
/// the order is the single record of what was charged and what was given back.
/// </summary>
public enum PaymentOrderStatus
{
    Pending,
    Paid,
    Failed,
    Expired,
    Cancelled,
    RefundRequested,
    Refunded,
    PartiallyRefunded
}
