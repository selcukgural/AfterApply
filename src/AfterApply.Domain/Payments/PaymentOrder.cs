using AfterApply.Domain.Common;

namespace AfterApply.Domain.Payments;

/// <summary>
/// One attempt to buy a Pro period through PayTR: what was asked for, what the customer typed for
/// the invoice, what PayTR answered, and what was refunded. The order is the ledger line the
/// entitlement, the receipt e-mail, the admin's monthly totals and every refund all hang off.
/// It outlives the account (<see cref="UserId"/> goes null on delete) because it is a financial
/// record that has to be kept for the statutory period; the billing fields stay for the same
/// reason and the privacy text says so.
/// </summary>
public sealed class PaymentOrder : AuditableEntity
{
    public const int MerchantOidLength = 32;
    public const int MaxEmailLength = 100;
    public const int MaxBillingNameLength = 60;
    public const int MaxBillingAddressLength = 400;
    public const int MaxBillingPhoneLength = 20;
    public const int MaxTermsVersionLength = 20;
    public const int MaxTokenLength = 100;
    public const int MaxFailedReasonLength = 500;
    public const int MaxRefundReasonLength = 500;
    public const int MaxRefundNoteLength = 500;
    public const int MaxReferenceNoLength = 64;
    public const int MaxPaymentTypeLength = 10;

    /// <summary>The get-token call itself was rejected or unreachable — no card was ever shown.
    /// Stored in <see cref="FailedReasonCode"/> so the admin list tells it apart from a bank refusal.</summary>
    public const int ProviderRejectedReasonCode = -1;

    public Guid? UserId { get; private set; }

    /// <summary>PayTR's <c>merchant_oid</c>: alphanumeric, unique, the only key the notification
    /// carries. A version-7 GUID in "N" form — no hyphens, sortable by time.</summary>
    public string MerchantOid { get; private set; } = string.Empty;

    public ProPlan Plan { get; private set; }

    /// <summary>What we asked PayTR to charge, in kuruş (the API takes amount × 100).</summary>
    public long AmountMinor { get; private set; }

    public string Currency { get; private set; } = "TL";

    public PaymentOrderStatus Status { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string BillingName { get; private set; } = string.Empty;

    public string BillingAddress { get; private set; } = string.Empty;

    public string BillingPhone { get; private set; } = string.Empty;

    public string Locale { get; private set; } = "tr";

    public DateTimeOffset TermsAcceptedAt { get; private set; }

    public string TermsVersion { get; private set; } = string.Empty;

    public string? IframeToken { get; private set; }

    public DateTimeOffset? TokenExpiresAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public int? FailedReasonCode { get; private set; }

    public string? FailedReasonMsg { get; private set; }

    /// <summary>What PayTR says it actually charged (<c>total_amount</c>). Equal to
    /// <see cref="AmountMinor"/> unless something unexpected happened, in which case
    /// <see cref="AmountMismatch"/> is raised for the admin.</summary>
    public long? TotalAmountMinor { get; private set; }

    public string? PaymentType { get; private set; }

    public bool TestMode { get; private set; }

    public bool AmountMismatch { get; private set; }

    /// <summary>The point this order's period was added onto (the old end when the entitlement
    /// was still running, otherwise the moment of payment) and the new end. The receipt shows the
    /// "after"; a full refund winds the entitlement back by the difference.</summary>
    public DateTimeOffset? EntitlementActiveUntilBefore { get; private set; }

    public DateTimeOffset? EntitlementActiveUntilAfter { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Null on a cancelled order means the user closed it themselves.</summary>
    public Guid? CancelledByUserId { get; private set; }

    public DateTimeOffset? RefundRequestedAt { get; private set; }

    public string? RefundReason { get; private set; }

    public DateTimeOffset? RefundedAt { get; private set; }

    /// <summary>Sum of every refund recorded against this order, in kuruş.</summary>
    public long RefundedAmountMinor { get; private set; }

    public string? RefundReferenceNo { get; private set; }

    public Guid? RefundedByUserId { get; private set; }

    public DateTimeOffset? RefundRejectedAt { get; private set; }

    public string? RefundRejectionNote { get; private set; }

    private PaymentOrder()
    {
    }

    public static PaymentOrder Create(
        Guid userId,
        ProPlan plan,
        long amountMinor,
        string currency,
        string email,
        string billingName,
        string billingAddress,
        string billingPhone,
        string locale,
        string termsVersion,
        DateTimeOffset now)
    {
        if (amountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinor), amountMinor, "A payment order must charge a positive amount.");
        }

        var id = Guid.CreateVersion7();
        return new PaymentOrder
        {
            Id = id,
            UserId = userId,
            MerchantOid = NewMerchantOid(id),
            Plan = plan,
            AmountMinor = amountMinor,
            Currency = currency,
            Status = PaymentOrderStatus.Pending,
            Email = email,
            BillingName = billingName,
            BillingAddress = billingAddress,
            BillingPhone = billingPhone,
            Locale = locale,
            TermsAcceptedAt = now,
            TermsVersion = termsVersion,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>The order id without hyphens — PayTR wants merchant_oid alphanumeric and at most
    /// 64 characters; keeping it derived from the id means the notification can be matched even
    /// from a log line that only shows one of them.</summary>
    public static string NewMerchantOid(Guid id) => id.ToString("N");

    public void AttachToken(string token, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        RequireStatus(PaymentOrderStatus.Pending, "attach a token to");
        IframeToken = token;
        TokenExpiresAt = expiresAt;
        Touch(now);
    }

    /// <summary>The get-token call was refused or never answered. Same terminal state as a bank
    /// refusal so the user's history reads the same, but with a reserved code and the raw reason
    /// kept for the admin.</summary>
    public void MarkProviderRejected(string reason, DateTimeOffset now)
    {
        RequireStatus(PaymentOrderStatus.Pending, "record a provider rejection on");
        Status = PaymentOrderStatus.Failed;
        FailedAt = now;
        FailedReasonCode = ProviderRejectedReasonCode;
        FailedReasonMsg = Truncate(reason, MaxFailedReasonLength);
        Touch(now);
    }

    /// <summary>Whether a notification of success would be new information, as opposed to a
    /// repeat of one already applied. Refund states count as paid.</summary>
    public bool IsPaid => Status is PaymentOrderStatus.Paid
        or PaymentOrderStatus.RefundRequested
        or PaymentOrderStatus.Refunded
        or PaymentOrderStatus.PartiallyRefunded;

    /// <summary>PayTR reported the charge. Allowed from every pre-payment state, not only Pending:
    /// a notification that arrives after we gave up on the window, or after the user pressed
    /// "cancel" and the bank finished anyway, still means the card was charged. Returns true when
    /// that late path was taken so the caller can log and flag it.</summary>
    public bool MarkPaid(long totalAmountMinor, string? paymentType, bool testMode, DateTimeOffset now)
    {
        if (IsPaid)
        {
            throw new PaymentOrderInvalidTransitionException(Status, "pay");
        }

        var late = Status != PaymentOrderStatus.Pending;
        Status = PaymentOrderStatus.Paid;
        PaidAt = now;
        TotalAmountMinor = totalAmountMinor;
        PaymentType = paymentType is null ? null : Truncate(paymentType, MaxPaymentTypeLength);
        TestMode = testMode;
        AmountMismatch = totalAmountMinor != AmountMinor;
        FailedAt = null;
        FailedReasonCode = null;
        FailedReasonMsg = null;
        Touch(now);
        return late;
    }

    public void RecordEntitlementChange(DateTimeOffset periodStart, DateTimeOffset after, DateTimeOffset now)
    {
        EntitlementActiveUntilBefore = periodStart;
        EntitlementActiveUntilAfter = after;
        Touch(now);
    }

    /// <summary>PayTR reported that the payment did not go through. From Expired or Cancelled it
    /// only adds the reason; from a paid state it is not a transition at all (the caller treats
    /// it as a duplicate).</summary>
    public void MarkFailed(int? reasonCode, string? reasonMsg, DateTimeOffset now)
    {
        if (Status is not (PaymentOrderStatus.Pending or PaymentOrderStatus.Expired or PaymentOrderStatus.Cancelled))
        {
            throw new PaymentOrderInvalidTransitionException(Status, "fail");
        }

        Status = PaymentOrderStatus.Failed;
        FailedAt = now;
        FailedReasonCode = reasonCode;
        FailedReasonMsg = reasonMsg is null ? null : Truncate(reasonMsg, MaxFailedReasonLength);
        Touch(now);
    }

    public void MarkExpired(DateTimeOffset now)
    {
        RequireStatus(PaymentOrderStatus.Pending, "expire");
        Status = PaymentOrderStatus.Expired;
        Touch(now);
    }

    public void Cancel(Guid? byUserId, DateTimeOffset now)
    {
        RequireStatus(PaymentOrderStatus.Pending, "cancel");
        Status = PaymentOrderStatus.Cancelled;
        CancelledAt = now;
        CancelledByUserId = byUserId;
        Touch(now);
    }

    /// <summary>What the customer actually paid — the ceiling for refunds.</summary>
    public long PaidAmountMinor => TotalAmountMinor ?? AmountMinor;

    public long RefundableAmountMinor => IsPaid ? Math.Max(0, PaidAmountMinor - RefundedAmountMinor) : 0;

    public void RequestRefund(string reason, DateTimeOffset now)
    {
        if (Status == PaymentOrderStatus.RefundRequested)
        {
            throw new PaymentRefundAlreadyRequestedException();
        }

        if (Status is not (PaymentOrderStatus.Paid or PaymentOrderStatus.PartiallyRefunded))
        {
            throw new PaymentRefundNotRefundableException();
        }

        Status = PaymentOrderStatus.RefundRequested;
        RefundRequestedAt = now;
        RefundReason = Truncate(reason, MaxRefundReasonLength);
        RefundRejectedAt = null;
        RefundRejectionNote = null;
        Touch(now);
    }

    public void RejectRefund(string note, DateTimeOffset now)
    {
        RequireStatus(PaymentOrderStatus.RefundRequested, "reject the refund request of");
        Status = RefundedAmountMinor > 0 ? PaymentOrderStatus.PartiallyRefunded : PaymentOrderStatus.Paid;
        RefundRejectedAt = now;
        RefundRejectionNote = Truncate(note, MaxRefundNoteLength);
        Touch(now);
    }

    /// <summary>A refund PayTR has confirmed (or one the admin verified in the merchant panel
    /// after our own commit failed). Partial refunds accumulate; the order is
    /// <see cref="PaymentOrderStatus.Refunded"/> once the whole paid amount is back.</summary>
    public void RecordRefund(long amountMinor, string? referenceNo, Guid byUserId, DateTimeOffset now)
    {
        if (!IsPaid)
        {
            throw new PaymentRefundNotRefundableException();
        }

        if (amountMinor <= 0 || amountMinor > RefundableAmountMinor)
        {
            throw new PaymentRefundExceedsTotalException();
        }

        RefundedAmountMinor += amountMinor;
        RefundedAt = now;
        RefundReferenceNo = referenceNo is null ? null : Truncate(referenceNo, MaxReferenceNoLength);
        RefundedByUserId = byUserId;
        Status = RefundedAmountMinor >= PaidAmountMinor ? PaymentOrderStatus.Refunded : PaymentOrderStatus.PartiallyRefunded;
        Touch(now);
    }

    /// <summary>How much this order extended the entitlement by — what a full refund takes back.</summary>
    public TimeSpan EntitlementExtension =>
        EntitlementActiveUntilAfter is { } after && EntitlementActiveUntilBefore is { } start && after > start
            ? after - start
            : TimeSpan.Zero;

    private void RequireStatus(PaymentOrderStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new PaymentOrderInvalidTransitionException(Status, action);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
