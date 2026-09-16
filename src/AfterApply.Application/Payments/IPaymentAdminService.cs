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

    Task<PaymentAlertsResponse> GetAlertsAsync(int days, CancellationToken cancellationToken);

    /// <summary>An admin closes a pending order. Null when it does not exist; throws when not pending.</summary>
    Task<AdminPaymentOrderResponse?> CancelAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken);
}

/// <param name="Status">A PaymentOrderStatus name, or null for all.</param>
/// <param name="Month">"yyyy-MM" of the order's creation, or null for all.</param>
/// <param name="Search">Matched against the e-mail and the merchant_oid.</param>
public sealed record PaymentOrderListQuery(string? Status, string? Month, string? Search, int Page, int PageSize);
