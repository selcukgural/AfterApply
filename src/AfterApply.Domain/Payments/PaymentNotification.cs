using AfterApply.Domain.Common;

namespace AfterApply.Domain.Payments;

/// <summary>
/// Every call PayTR makes to our notification URL, and every refund we record, as it arrived —
/// including the ones we rejected. Append-only evidence: when the merchant panel shows an order
/// as "Devam ediyor" or a customer disputes a charge, this table says what we received and what
/// we did with it. Holds no card data (PayTR never sends any) and no user id; the order links it.
/// The hash value itself is left out of <see cref="RawForm"/> — it proves nothing once verified.
/// </summary>
public sealed class PaymentNotification : Entity
{
    public const int MaxRawFormLength = 4000;
    public const int MaxStatusLength = 20;

    public string MerchantOid { get; private set; } = string.Empty;

    public Guid? OrderId { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public long? TotalAmountMinor { get; private set; }

    public long? PaymentAmountMinor { get; private set; }

    public string? PaymentType { get; private set; }

    public int? FailedReasonCode { get; private set; }

    public string? FailedReasonMsg { get; private set; }

    public bool TestMode { get; private set; }

    public bool HashValid { get; private set; }

    public PaymentNotificationOutcome Outcome { get; private set; }

    public string RawForm { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    private PaymentNotification()
    {
    }

    public static PaymentNotification Create(
        string merchantOid,
        Guid? orderId,
        string status,
        long? totalAmountMinor,
        long? paymentAmountMinor,
        string? paymentType,
        int? failedReasonCode,
        string? failedReasonMsg,
        bool testMode,
        bool hashValid,
        PaymentNotificationOutcome outcome,
        string rawForm,
        DateTimeOffset now) =>
        new()
        {
            MerchantOid = Truncate(merchantOid, PaymentOrder.MaxReferenceNoLength),
            OrderId = orderId,
            Status = Truncate(status, MaxStatusLength),
            TotalAmountMinor = totalAmountMinor,
            PaymentAmountMinor = paymentAmountMinor,
            PaymentType = paymentType is null ? null : Truncate(paymentType, PaymentOrder.MaxPaymentTypeLength),
            FailedReasonCode = failedReasonCode,
            FailedReasonMsg = failedReasonMsg is null ? null : Truncate(failedReasonMsg, PaymentOrder.MaxFailedReasonLength),
            TestMode = testMode,
            HashValid = hashValid,
            Outcome = outcome,
            RawForm = Truncate(rawForm, MaxRawFormLength),
            ReceivedAt = now
        };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

public enum PaymentNotificationOutcome
{
    /// <summary>A genuine notification applied to a pending order.</summary>
    Applied,
    /// <summary>A repeat of one already applied; answered "OK" and otherwise ignored.</summary>
    Duplicate,
    /// <summary>Success reported for an order we had already expired, cancelled or failed — honoured.</summary>
    LateApplied,
    /// <summary>Valid hash, but no order with that merchant_oid — answered non-OK so PayTR keeps retrying.</summary>
    UnknownOrder,
    /// <summary>The hash did not verify; nothing was changed.</summary>
    BadHash,
    /// <summary>A required field was missing or unparseable.</summary>
    Malformed,
    /// <summary>We threw while applying it; answered non-OK so PayTR retries.</summary>
    Error,
    /// <summary>Not a notification: a refund we sent through the refund API, with PayTR's answer.</summary>
    RefundRecorded
}
