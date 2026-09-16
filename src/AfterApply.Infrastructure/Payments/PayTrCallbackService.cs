using System.Text.Json;
using AfterApply.Application.Mailing;
using AfterApply.Application.Payments;
using AfterApply.Application.Pro;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// Step 2 of the iFrame flow. Verifies the HMAC, finds the order by merchant_oid, applies the
/// result once, and answers PayTR. The answer discipline is the whole point: "OK" tells PayTR to
/// stop, so it is only sent once the change is committed (or when there is nothing left to
/// change, or when a retry could never change anything — an order this database has never
/// had); anything that goes wrong on our side is a non-OK answer, which makes PayTR retry and
/// show the transaction as "Devam ediyor" in the merchant panel until we get it right. Every
/// notification is logged to PaymentNotifications in its own scope, so the evidence survives a
/// failed transaction.
/// </summary>
internal sealed class PayTrCallbackService(
    AppDbContext dbContext,
    IOptions<PayTrOptions> options,
    IOptions<AppOptions> appOptions,
    IProEntitlementService entitlements,
    IBackgroundJobClient jobClient,
    IServiceScopeFactory scopeFactory,
    ILogger<PayTrCallbackService> logger,
    TimeProvider? timeProvider = null) : IPayTrCallbackService
{
    private const int ConcurrencyAttempts = 3;

    private readonly PayTrOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PayTrCallbackResult> HandleAsync(IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var fields = CallbackFields.From(form);
        var rawForm = Serialize(form);

        if (!fields.IsComplete)
        {
            logger.LogWarning("PayTR notification missing fields (oid {MerchantOid})", fields.MerchantOid);
            await RecordAsync(fields, null, hashValid: false, PaymentNotificationOutcome.Malformed, rawForm, now);
            return PayTrCallbackResult.Reject(400, "missing field");
        }

        if (!_options.IsConfigured ||
            !PayTrSignature.VerifyCallback(fields.MerchantOid, _options.MerchantSalt!, fields.Status, fields.TotalAmountText, _options.MerchantKey!, fields.Hash))
        {
            logger.LogWarning("PayTR notification with a bad hash for oid {MerchantOid}", fields.MerchantOid);
            await RecordAsync(fields, null, hashValid: false, PaymentNotificationOutcome.BadHash, rawForm, now);
            return PayTrCallbackResult.Reject(400, "bad hash");
        }

        Guid? orderId = null;
        var outcome = PaymentNotificationOutcome.Error;
        try
        {
            for (var attempt = 1; attempt <= ConcurrencyAttempts; attempt++)
            {
                try
                {
                    var (result, appliedOutcome, id) = await ApplyOnceAsync(fields, now, cancellationToken);
                    orderId = id;
                    outcome = appliedOutcome;
                    return result;
                }
                catch (DbUpdateException ex) when (attempt < ConcurrencyAttempts)
                {
                    // Another instance applied a notification for the same order between our read
                    // and our write — seen either as the row version having moved
                    // (DbUpdateConcurrencyException) or as the entitlement's unique index refusing
                    // a second row. Read again and the duplicate check will answer.
                    logger.LogInformation(ex, "PayTR notification for oid {MerchantOid} raced another write; retrying ({Attempt})",
                        fields.MerchantOid, attempt);
                    dbContext.ChangeTracker.Clear();
                }
            }

            throw new InvalidOperationException("Gave up after repeated concurrency conflicts.");
        }
        catch (Exception ex)
        {
            outcome = PaymentNotificationOutcome.Error;
            logger.LogError(ex, "PayTR notification for oid {MerchantOid} could not be applied; answering non-OK so PayTR retries", fields.MerchantOid);
            return PayTrCallbackResult.Reject(500, "internal error");
        }
        finally
        {
            await RecordAsync(fields, orderId, hashValid: true, outcome, rawForm, now);
        }
    }

    private async Task<(PayTrCallbackResult Result, PaymentNotificationOutcome Outcome, Guid? OrderId)> ApplyOnceAsync(
        CallbackFields fields, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.MerchantOid == fields.MerchantOid, cancellationToken);
        if (order is null)
        {
            // A valid hash we cannot match: recorded as UnknownOrder (the admin alerts list shows
            // it) and answered OK. The checkout writes the order before it asks PayTR for a token,
            // so in this database the order can only be missing because the transaction was made
            // against another environment (the local test payments, replayed by the merchant
            // panel's live-mode check on 2026-09-16); a retry can never find it, and a non-OK
            // answer only kept the panel's check red. The evidence log is the durable signal.
            logger.LogError("PayTR notification for unknown oid {MerchantOid} ({Status}); recorded, answered OK", fields.MerchantOid, fields.Status);
            return (PayTrCallbackResult.Ok, PaymentNotificationOutcome.UnknownOrder, null);
        }

        if (order.IsPaid)
        {
            return (PayTrCallbackResult.Ok, PaymentNotificationOutcome.Duplicate, order.Id);
        }

        if (fields.IsSuccess)
        {
            var totalMinor = fields.TotalAmountMinor ?? order.AmountMinor;
            var late = order.MarkPaid(totalMinor, fields.PaymentType, fields.TestMode, now);
            if (order.AmountMismatch)
            {
                logger.LogWarning("PayTR charged {Total} for order {OrderId} but {Expected} was asked; applied and flagged",
                    totalMinor, order.Id, order.AmountMinor);
            }

            DateTimeOffset? activeUntil = null;
            if (order.UserId is { } userId)
            {
                var extension = await entitlements.ExtendForPaymentAsync(userId, order.Plan, cancellationToken);
                order.RecordEntitlementChange(extension.PeriodStart, extension.ActiveUntil, now);
                activeUntil = extension.ActiveUntil;
            }
            else
            {
                logger.LogWarning("PayTR success for order {OrderId} whose account is gone; recorded, no entitlement", order.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (late)
            {
                logger.LogWarning("PayTR success applied late to order {OrderId} (was not pending)", order.Id);
            }
            else
            {
                logger.LogInformation("PayTR payment confirmed for order {OrderId}", order.Id);
            }

            if (activeUntil is { } until)
            {
                EnqueueReceipt(order, until);
            }

            return (PayTrCallbackResult.Ok, late ? PaymentNotificationOutcome.LateApplied : PaymentNotificationOutcome.Applied, order.Id);
        }

        if (order.Status == PaymentOrderStatus.Failed)
        {
            return (PayTrCallbackResult.Ok, PaymentNotificationOutcome.Duplicate, order.Id);
        }

        order.MarkFailed(fields.FailedReasonCode, fields.FailedReasonMsg, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("PayTR payment failed for order {OrderId}: code {Code}", order.Id, fields.FailedReasonCode);
        return (PayTrCallbackResult.Ok, PaymentNotificationOutcome.Applied, order.Id);
    }

    private void EnqueueReceipt(PaymentOrder order, DateTimeOffset activeUntil)
    {
        var locale = PaymentFormatting.NormalizeLocale(order.Locale);
        var receipt = new PaymentReceipt(
            PaymentFormatting.PlanName(order.Plan, locale),
            PaymentFormatting.Amount(order.PaidAmountMinor, locale),
            PaymentFormatting.Date(activeUntil, locale),
            $"{appOptions.Value.WebBaseUrl.TrimEnd('/')}/{locale}/pro");
        var email = order.Email;
        jobClient.Enqueue<IEmailSender>(s => s.SendPaymentReceivedEmailAsync(email, locale, receipt, CancellationToken.None));
    }

    // Written in its own scope and never allowed to throw: the evidence row must exist whether
    // the main transaction committed or not, and a logging failure must not turn an applied
    // payment into a non-OK answer.
    private async Task RecordAsync(CallbackFields fields, Guid? orderId, bool hashValid, PaymentNotificationOutcome outcome, string rawForm, DateTimeOffset now)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PaymentNotifications.Add(PaymentNotification.Create(fields.MerchantOid, orderId, fields.Status, fields.TotalAmountMinor,
                fields.PaymentAmountMinor, fields.PaymentType, fields.FailedReasonCode, fields.FailedReasonMsg, fields.TestMode, hashValid,
                outcome, rawForm, now));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not record PayTR notification for oid {MerchantOid}", fields.MerchantOid);
        }
    }

    // The hash proves nothing once verified and is the one field that could be replayed, so it
    // stays out of the stored copy.
    private static string Serialize(IReadOnlyDictionary<string, string> form) =>
        JsonSerializer.Serialize(form.Where(kv => kv.Key != "hash").OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value));

    private sealed record CallbackFields(
        string MerchantOid,
        string Status,
        string TotalAmountText,
        string Hash,
        long? TotalAmountMinor,
        long? PaymentAmountMinor,
        string? PaymentType,
        int? FailedReasonCode,
        string? FailedReasonMsg,
        bool TestMode)
    {
        public bool IsComplete => MerchantOid.Length is > 0 and <= PaymentOrder.MaxReferenceNoLength
                                  && Status.Length > 0 && TotalAmountText.Length > 0 && Hash.Length > 0;

        public bool IsSuccess => Status == "success";

        public static CallbackFields From(IReadOnlyDictionary<string, string> form)
        {
            string Get(string key) => form.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
            string? GetOrNull(string key) => form.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

            return new CallbackFields(
                Get("merchant_oid"),
                Get("status"),
                Get("total_amount"),
                Get("hash"),
                PayTrMoney.ParseMinor(GetOrNull("total_amount")),
                PayTrMoney.ParseMinor(GetOrNull("payment_amount")),
                GetOrNull("payment_type"),
                int.TryParse(GetOrNull("failed_reason_code"), out var code) ? code : null,
                GetOrNull("failed_reason_msg"),
                GetOrNull("test_mode") == "1");
        }
    }
}
