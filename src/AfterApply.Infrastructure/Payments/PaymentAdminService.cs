using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Payments;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

internal sealed class PaymentAdminService(AppDbContext dbContext, IOptions<PayTrOptions> options, TimeProvider? timeProvider = null) : IPaymentAdminService
{
    private const int MaxPageSize = 100;
    private const int MaxMonths = 36;

    private static readonly PaymentNotificationOutcome[] AlertOutcomes =
    [
        PaymentNotificationOutcome.BadHash, PaymentNotificationOutcome.UnknownOrder, PaymentNotificationOutcome.LateApplied,
        PaymentNotificationOutcome.Malformed, PaymentNotificationOutcome.Error
    ];

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PaymentsSummaryResponse> GetSummaryAsync(int months, CancellationToken cancellationToken)
    {
        months = Math.Clamp(months, 1, MaxMonths);
        var now = _timeProvider.GetUtcNow();
        var since = MonthlySummary.MonthStart(now, MonthlySummary.MerchantTimeZone).AddMonths(-(months - 1));

        // Anything that moved in the window: paid, failed or closed since the first month began.
        var orders = await dbContext.PaymentOrders.AsNoTracking()
            .Where(o => o.UpdatedAt >= since)
            .Select(o => new LedgerOrder(
                o.PaidAt,
                o.TotalAmountMinor,
                o.Status == PaymentOrderStatus.Failed ? o.FailedAt : null,
                o.Status == PaymentOrderStatus.Cancelled ? o.CancelledAt : o.Status == PaymentOrderStatus.Expired ? o.UpdatedAt : null,
                o.TestMode))
            .ToListAsync(cancellationToken);

        var refunds = await dbContext.PaymentNotifications.AsNoTracking()
            .Where(n => n.Outcome == PaymentNotificationOutcome.RefundRecorded && n.ReceivedAt >= since)
            .Select(n => new LedgerRefund(n.ReceivedAt, n.TotalAmountMinor ?? 0, n.TestMode))
            .ToListAsync(cancellationToken);

        var summary = MonthlySummary.Compute(orders, refunds, now, months);
        var openRefundRequests = await dbContext.PaymentOrders.CountAsync(o => o.Status == PaymentOrderStatus.RefundRequested, cancellationToken);
        var activePro = await dbContext.ProEntitlements.CountAsync(e => e.RevokedAt == null && e.ActiveUntil > now, cancellationToken);
        var testOrders = await dbContext.PaymentOrders.CountAsync(o => o.TestMode && o.PaidAt >= now.AddDays(-30), cancellationToken);

        return new PaymentsSummaryResponse(options.Value.Currency, summary[0], summary, openRefundRequests, activePro, testOrders);
    }

    public async Task<PagedResult<AdminPaymentOrderResponse>> ListOrdersAsync(PaymentOrderListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var orders = dbContext.PaymentOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PaymentOrderStatus>(query.Status, ignoreCase: true, out var status))
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Month) && TryParseMonth(query.Month, out var monthStart))
        {
            var monthEnd = MonthlySummary.MonthStart(monthStart.AddDays(40), MonthlySummary.MerchantTimeZone);
            orders = orders.Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            orders = orders.Where(o => o.Email.Contains(term) || o.MerchantOid == term.ToLowerInvariant());
        }

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminPaymentOrderResponse>(items.Select(o => o.ToAdminResponse(_timeProvider.GetUtcNow())).ToList(), total, page, pageSize);
    }

    public async Task<AdminPaymentOrderDetailResponse?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var notifications = await dbContext.PaymentNotifications.AsNoTracking()
            .Where(n => n.MerchantOid == order.MerchantOid)
            .OrderBy(n => n.ReceivedAt)
            .ToListAsync(cancellationToken);

        PaymentEntitlementResponse? entitlement = null;
        if (order.UserId is { } userId)
        {
            var now = _timeProvider.GetUtcNow();
            var row = await dbContext.ProEntitlements.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
            entitlement = row is null ? null : new PaymentEntitlementResponse(row.IsActive(now), row.ActiveUntil);
        }

        return new AdminPaymentOrderDetailResponse(order.ToAdminResponse(_timeProvider.GetUtcNow()), notifications.Select(n => n.ToResponse()).ToList(), entitlement);
    }

    public async Task<IReadOnlyList<AdminPaymentOrderResponse>> ListRefundRequestsAsync(CancellationToken cancellationToken)
    {
        var orders = await dbContext.PaymentOrders.AsNoTracking()
            .Where(o => o.Status == PaymentOrderStatus.RefundRequested)
            .OrderBy(o => o.RefundRequestedAt)
            .ToListAsync(cancellationToken);
        return orders.Select(o => o.ToAdminResponse(_timeProvider.GetUtcNow())).ToList();
    }

    public async Task<PaymentAlertsResponse> GetAlertsAsync(int days, CancellationToken cancellationToken)
    {
        var since = _timeProvider.GetUtcNow().AddDays(-Math.Clamp(days, 1, 365));
        var notifications = await dbContext.PaymentNotifications.AsNoTracking()
            .Where(n => n.ReceivedAt >= since && AlertOutcomes.Contains(n.Outcome))
            .OrderByDescending(n => n.ReceivedAt).ThenByDescending(n => n.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
        var mismatches = await dbContext.PaymentOrders.AsNoTracking()
            .Where(o => o.AmountMismatch && o.PaidAt >= since)
            .OrderByDescending(o => o.PaidAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return new PaymentAlertsResponse(notifications.Select(n => n.ToResponse()).ToList(), mismatches.Select(o => o.ToAdminResponse(_timeProvider.GetUtcNow())).ToList());
    }

    public async Task<AdminPaymentOrderResponse?> CancelAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.Cancel(adminUserId, _timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return order.ToAdminResponse(_timeProvider.GetUtcNow());
    }

    private static bool TryParseMonth(string month, out DateTimeOffset start)
    {
        start = default;
        var parts = month.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var m) || m is < 1 or > 12)
        {
            return false;
        }

        var local = new DateTime(year, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
        start = new DateTimeOffset(local, MonthlySummary.MerchantTimeZone.GetUtcOffset(local)).ToUniversalTime();
        return true;
    }
}
