namespace AfterApply.Application.Payments.Contracts;

public sealed record PaymentPlansResponse(
    string Currency,
    string TermsVersion,
    IReadOnlyList<PaymentPlanResponse> Plans,
    PaymentEntitlementResponse Entitlement);

public sealed record PaymentPlanResponse(string Plan, long AmountMinor, int Months);

public sealed record PaymentEntitlementResponse(bool IsActive, DateTimeOffset? ActiveUntil);

/// <summary>What the checkout page needs to show the payment frame. The iframe URL carries
/// PayTR's single-use token; nothing else of PayTR's is exposed.</summary>
public sealed record CheckoutResponse(
    Guid OrderId,
    string MerchantOid,
    string IframeUrl,
    DateTimeOffset ExpiresAt,
    string Plan,
    long AmountMinor,
    string Currency);

/// <summary>The user's own view of an order — enough for the result page and the history list,
/// no billing echo (they typed it) and no provider internals.</summary>
public sealed record PaymentOrderResponse(
    Guid Id,
    string Plan,
    long AmountMinor,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? TokenExpiresAt,
    int? FailedReasonCode,
    string? FailedReasonMsg,
    long RefundedAmountMinor,
    DateTimeOffset? RefundRequestedAt,
    DateTimeOffset? EntitlementActiveUntil,
    bool CanRequestRefund);

public sealed record AdminPaymentOrderResponse(
    Guid Id,
    Guid? UserId,
    string Email,
    string MerchantOid,
    string Plan,
    long AmountMinor,
    long? TotalAmountMinor,
    string Currency,
    string Status,
    string BillingName,
    string BillingAddress,
    string BillingPhone,
    string Locale,
    string TermsVersion,
    DateTimeOffset TermsAcceptedAt,
    string? PaymentType,
    bool TestMode,
    bool AmountMismatch,
    int? FailedReasonCode,
    string? FailedReasonMsg,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? TokenExpiresAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? CancelledAt,
    Guid? CancelledByUserId,
    DateTimeOffset? EntitlementActiveUntilBefore,
    DateTimeOffset? EntitlementActiveUntilAfter,
    DateTimeOffset? RefundRequestedAt,
    string? RefundReason,
    DateTimeOffset? RefundedAt,
    long RefundedAmountMinor,
    long RefundableAmountMinor,
    string? RefundReferenceNo,
    Guid? RefundedByUserId,
    DateTimeOffset? RefundRejectedAt,
    string? RefundRejectionNote);

public sealed record PaymentNotificationResponse(
    Guid Id,
    string MerchantOid,
    Guid? OrderId,
    string Status,
    long? TotalAmountMinor,
    long? PaymentAmountMinor,
    string? PaymentType,
    int? FailedReasonCode,
    string? FailedReasonMsg,
    bool TestMode,
    bool HashValid,
    string Outcome,
    DateTimeOffset ReceivedAt);

public sealed record AdminPaymentOrderDetailResponse(
    AdminPaymentOrderResponse Order,
    IReadOnlyList<PaymentNotificationResponse> Notifications,
    PaymentEntitlementResponse? Entitlement);

/// <summary>One calendar month of the admin's ledger. Gross is what PayTR reported charging
/// (KDV included, PayTR's commission not deducted) for orders paid in the month; refunds are
/// counted in the month they were made; net is the difference. Test-mode orders are excluded.</summary>
public sealed record PaymentsMonthSummary(
    string Month,
    int PaidCount,
    long GrossMinor,
    long RefundedMinor,
    int RefundCount,
    int CancelledCount,
    int FailedCount,
    long NetMinor);

public sealed record PaymentsSummaryResponse(
    string Currency,
    PaymentsMonthSummary CurrentMonth,
    IReadOnlyList<PaymentsMonthSummary> Months,
    int OpenRefundRequests,
    int ActiveProUsers,
    int TestOrdersLast30Days);

/// <summary>What deserves a look: notifications we rejected or applied late, and paid orders whose
/// charged amount differed from what we asked for.</summary>
public sealed record PaymentAlertsResponse(
    IReadOnlyList<PaymentNotificationResponse> Notifications,
    IReadOnlyList<AdminPaymentOrderResponse> AmountMismatches);
