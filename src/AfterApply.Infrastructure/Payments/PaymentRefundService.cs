using System.Text.Json;
using AfterApply.Application.Common;
using AfterApply.Application.Mailing;
using AfterApply.Application.Payments;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Application.Pro;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// Refunds through the PayTR refund API, recorded on the order and mirrored into
/// PaymentNotifications. Order of operations in <see cref="RefundAsync"/> matters: PayTR is
/// called first and outside any transaction (its answer is the fact), then the order, the
/// entitlement and the evidence row commit together, then the e-mail is queued. If the commit
/// fails after PayTR said yes, the money has moved and our row has not — the log says so, a second
/// attempt is refused by PayTR ("toplam iade tutarı ... fazla olamaz"), and the admin records it
/// with <see cref="MarkRefundedAsync"/> after checking the merchant panel.
/// </summary>
internal sealed class PaymentRefundService(
    AppDbContext dbContext,
    IPayTrClient payTrClient,
    IProEntitlementService entitlements,
    IBackgroundJobClient jobClient,
    ILogger<PaymentRefundService> logger,
    TimeProvider? timeProvider = null) : IPaymentRefundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PaymentOrderResponse?> RequestAsync(Guid userId, Guid orderId, string reason, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.RequestRefund(reason.Trim(), _timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refund requested for order {OrderId}", order.Id);
        return order.ToUserResponse();
    }

    public async Task<AdminPaymentOrderResponse?> RejectAsync(Guid adminUserId, Guid orderId, string note, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.RejectRefund(note.Trim(), _timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refund request rejected for order {OrderId} by {AdminId}", order.Id, adminUserId);

        var (email, locale) = (order.Email, PaymentFormatting.NormalizeLocale(order.Locale));
        var trimmedNote = note.Trim();
        jobClient.Enqueue<IEmailSender>(s => s.SendRefundRejectedEmailAsync(email, locale, trimmedNote, CancellationToken.None));
        return order.ToAdminResponse();
    }

    public async Task<AdminPaymentOrderResponse?> RefundAsync(Guid adminUserId, Guid orderId, long? amountMinor, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (!order.IsPaid)
        {
            throw new PaymentRefundNotRefundableException();
        }

        var amount = amountMinor ?? order.RefundableAmountMinor;
        if (amount <= 0 || amount > order.RefundableAmountMinor)
        {
            throw new PaymentRefundExceedsTotalException();
        }

        // Our own id as PayTR's reference_no: a second call with the same reference is refused on
        // their side too, which is one more guard against refunding twice.
        var referenceNo = order.Id.ToString("N");
        var result = await payTrClient.RefundAsync(order.MerchantOid, amount, referenceNo, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        switch (result)
        {
            case PayTrRefundResult.Success success:
                await CommitRefundAsync(order, amount, success.ReferenceNo ?? referenceNo, adminUserId, now,
                    JsonSerializer.Serialize(new { success.ReturnAmount, success.ReferenceNo, success.IsTest }), cancellationToken);
                logger.LogInformation("Refunded {Amount} kuruş on order {OrderId} by {AdminId}", amount, order.Id, adminUserId);
                return order.ToAdminResponse();
            case PayTrRefundResult.Rejected rejected:
                await RecordAsync(order, amount, PaymentNotificationOutcome.Error,
                    JsonSerializer.Serialize(new { rejected.ErrorNo, rejected.Message }), now);
                throw new CodedException("PAYMENT_REFUND_PROVIDER_ERROR", $"PayTR refused the refund: {rejected.ErrorNo} {rejected.Message}",
                    $"{rejected.ErrorNo} {rejected.Message}".Trim());
            case PayTrRefundResult.Unavailable unavailable:
                await RecordAsync(order, amount, PaymentNotificationOutcome.Error, JsonSerializer.Serialize(new { unavailable.Reason }), now);
                throw new CodedException("PAYMENT_REFUND_PROVIDER_ERROR", $"PayTR unreachable: {unavailable.Reason}", unavailable.Reason);
            default:
                throw new InvalidOperationException("Unknown PayTR refund result.");
        }
    }

    public async Task<AdminPaymentOrderResponse?> MarkRefundedAsync(Guid adminUserId, Guid orderId, long amountMinor, string referenceNo,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        await CommitRefundAsync(order, amountMinor, referenceNo.Trim(), adminUserId, now,
            JsonSerializer.Serialize(new { markedByHand = true, referenceNo }), cancellationToken);
        logger.LogWarning("Refund of {Amount} kuruş on order {OrderId} recorded by hand by {AdminId} (reference {Reference})",
            amountMinor, order.Id, adminUserId, referenceNo);
        return order.ToAdminResponse();
    }

    private async Task CommitRefundAsync(PaymentOrder order, long amountMinor, string referenceNo, Guid adminUserId, DateTimeOffset now,
        string rawAnswer, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        order.RecordRefund(amountMinor, referenceNo, adminUserId, now);
        if (order.Status == PaymentOrderStatus.Refunded && order.UserId is { } userId)
        {
            // A full refund takes back exactly the period this order added; days from other,
            // unrefunded orders stay.
            await entitlements.WindBackAsync(userId, order.EntitlementExtension, cancellationToken);
        }

        dbContext.PaymentNotifications.Add(PaymentNotification.Create(order.MerchantOid, order.Id, "refund", amountMinor, order.PaidAmountMinor,
            order.PaymentType, null, null, order.TestMode, hashValid: true, PaymentNotificationOutcome.RefundRecorded, rawAnswer, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var (email, locale) = (order.Email, PaymentFormatting.NormalizeLocale(order.Locale));
        var amountText = PaymentFormatting.Amount(amountMinor, locale);
        jobClient.Enqueue<IEmailSender>(s => s.SendRefundCompletedEmailAsync(email, locale, amountText, CancellationToken.None));
    }

    private async Task RecordAsync(PaymentOrder order, long amountMinor, PaymentNotificationOutcome outcome, string rawAnswer, DateTimeOffset now)
    {
        dbContext.PaymentNotifications.Add(PaymentNotification.Create(order.MerchantOid, order.Id, "refund", amountMinor, order.PaidAmountMinor,
            order.PaymentType, null, null, order.TestMode, hashValid: true, outcome, rawAnswer, now));
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }
}
