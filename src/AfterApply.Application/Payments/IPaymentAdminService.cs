using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Payments.Contracts;

namespace AfterApply.Application.Payments;

/// <summary>The admin's payments panel: monthly totals, the order list, the refund queue, alerts.</summary>
public interface IPaymentAdminService
{
    Task<PaymentsSummaryResponse> GetSummaryAsync(int months, CancellationToken cancellationToken);

    Task<PagedResult<AdminPaymentOrderResponse>> ListOrdersAsync(PaymentOrderListQuery query, CancellationToken cancellationToken);

    Task<AdminPaymentOrderDetailResponse?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminPaymentOrderResponse>> ListRefundRequestsAsync(CancellationToken cancellationToken);

    Task<PaymentAlertsResponse> GetAlertsAsync(PaymentAlertListQuery query, CancellationToken cancellationToken);

    /// <summary>An admin closes a pending order. Null when it does not exist; throws when not pending.</summary>
    Task<AdminPaymentOrderResponse?> CancelAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken);
}

/// <param name="Status">A PaymentOrderStatus name, or null for all.</param>
/// <param name="Month">"yyyy-MM" of the order's creation, or null for all.</param>
/// <param name="Search">Matched against the e-mail and the merchant_oid.</param>
public sealed record PaymentOrderListQuery(string? Status, string? Month, string? Search, int Page, int PageSize);

/// <param name="Days">How far back to look; clamped to 1..365, 30 when absent.</param>
/// <param name="Outcome">A PaymentNotificationOutcome name, or null for every alert outcome. One that is
/// not an alert outcome (Applied, Duplicate, RefundRecorded) matches nothing — they are never alerts.</param>
/// <param name="Status">PayTR's status word ("success" / "failed"), "refund" for a refund call PayTR refused, or null for all.</param>
/// <param name="TestMode">true for test-mode rows only, false for live only, null for both.</param>
/// <param name="Search">Matched inside the merchant_oid.</param>
/// <param name="Sort">See <see cref="PaymentAlertList.ParseSort"/>.</param>
public sealed record PaymentAlertListQuery(
    int? Days,
    string? Outcome,
    string? Status,
    bool? TestMode,
    string? Search,
    string? Sort,
    string? Direction,
    int Page,
    int? PageSize);
