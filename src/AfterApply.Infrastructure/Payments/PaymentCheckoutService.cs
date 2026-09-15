using AfterApply.Application.Common;
using AfterApply.Application.Payments;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// Step 1 of the iFrame flow. The order row is written before PayTR is called, so a crash between
/// the two still leaves something for the notification to find; PayTR is called with no database
/// transaction open (Cloud SQL has few connection slots, and a 15-second HTTP call must not hold one).
/// </summary>
internal sealed class PaymentCheckoutService(
    AppDbContext dbContext,
    IPayTrClient payTrClient,
    IOptions<PayTrOptions> options,
    IOptions<AppOptions> appOptions,
    IHostEnvironment environment,
    ILogger<PaymentCheckoutService> logger,
    TimeProvider? timeProvider = null) : IPaymentCheckoutService
{
    /// <summary>A pending order with at least this much of its window left is resumed instead of
    /// replaced when the user reloads the checkout — the same iframe comes back.</summary>
    private static readonly TimeSpan ResumeMinimumRemaining = TimeSpan.FromMinutes(5);

    private readonly PayTrOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PaymentPlansResponse> GetPlansAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var activeUntil = await dbContext.ProEntitlements.AsNoTracking()
            .Where(e => e.UserId == userId && e.RevokedAt == null && e.ActiveUntil > now)
            .Select(e => (DateTimeOffset?)e.ActiveUntil)
            .FirstOrDefaultAsync(cancellationToken);

        var plans = new List<PaymentPlanResponse>
        {
            new(ProPlan.Monthly.ToString(), _options.Plans.Monthly.AmountMinor, 1),
            new(ProPlan.Yearly.ToString(), _options.Plans.Yearly.AmountMinor, 12)
        };
        return new PaymentPlansResponse(_options.Currency, _options.TermsVersion, plans.Where(p => p.AmountMinor > 0).ToList(),
            new PaymentEntitlementResponse(activeUntil is not null, activeUntil));
    }

    public async Task<CheckoutResponse> StartAsync(Guid userId, StartCheckoutRequest request, string? clientIp, string locale,
        CancellationToken cancellationToken)
    {
        var plan = Enum.Parse<ProPlan>(request.Plan, ignoreCase: true);
        var amount = PriceOf(plan);
        if (amount <= 0)
        {
            throw new CodedException("PAYMENT_PLAN_UNKNOWN", $"Plan {plan} is not for sale.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CodedException("PAYMENT_PROVIDER_UNAVAILABLE", "User not found.");
        var email = user.Email ?? string.Empty;
        if (!PaymentFormatting.IsAsciiEmail(email))
        {
            throw new CodedException("PAYMENT_EMAIL_UNSUPPORTED", "PayTR does not accept this e-mail address.");
        }

        locale = PaymentFormatting.NormalizeLocale(locale);
        var now = _timeProvider.GetUtcNow();

        var resumable = await dbContext.PaymentOrders
            .Where(o => o.UserId == userId && o.Status == PaymentOrderStatus.Pending && o.Plan == plan
                        && o.IframeToken != null && o.TokenExpiresAt > now + ResumeMinimumRemaining
                        && o.AmountMinor == amount)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (resumable is not null)
        {
            return ToCheckout(resumable);
        }

        var order = PaymentOrder.Create(userId, plan, amount, _options.Currency, email, request.BillingName.Trim(),
            request.BillingAddress.Trim(), request.BillingPhone.Trim(), locale, _options.TermsVersion, now);
        dbContext.PaymentOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var userIp = ResolveUserIp(clientIp);
        var webBase = appOptions.Value.WebBaseUrl.TrimEnd('/');
        var tokenRequest = new PayTrTokenRequest(
            userIp,
            order.MerchantOid,
            email,
            amount,
            PaymentFormatting.PlanName(plan, locale),
            order.BillingName,
            order.BillingAddress,
            order.BillingPhone,
            $"{webBase}/{locale}/pro/return/{order.Id}?outcome=ok",
            $"{webBase}/{locale}/pro/return/{order.Id}?outcome=fail",
            locale);

        var result = await payTrClient.GetIframeTokenAsync(tokenRequest, cancellationToken);
        switch (result)
        {
            case PayTrTokenResult.Success success:
                order.AttachToken(success.Token, now + _options.TimeoutLimit, now);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("PayTR checkout started: order {OrderId} plan {Plan}", order.Id, plan);
                return ToCheckout(order);
            case PayTrTokenResult.Rejected rejected:
                order.MarkProviderRejected(rejected.Reason, now);
                await dbContext.SaveChangesAsync(cancellationToken);
                throw new CodedException("PAYMENT_PROVIDER_UNAVAILABLE", $"PayTR rejected the token request: {rejected.Reason}");
            case PayTrTokenResult.Unavailable unavailable:
                order.MarkProviderRejected(unavailable.Reason, now);
                await dbContext.SaveChangesAsync(cancellationToken);
                throw new CodedException("PAYMENT_PROVIDER_UNAVAILABLE", $"PayTR unreachable: {unavailable.Reason}");
            default:
                throw new InvalidOperationException("Unknown PayTR token result.");
        }
    }

    public async Task<PaymentOrderResponse?> GetOrderAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);
        return order?.ToUserResponse();
    }

    public async Task<IReadOnlyList<PaymentOrderResponse>> ListOrdersAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        var orders = await dbContext.PaymentOrders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return orders.Select(o => o.ToUserResponse()).ToList();
    }

    public async Task<bool> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.SingleOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        order.Cancel(byUserId: null, _timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private long PriceOf(ProPlan plan) => plan switch
    {
        ProPlan.Monthly => _options.Plans.Monthly.AmountMinor,
        ProPlan.Yearly => _options.Plans.Yearly.AmountMinor,
        _ => 0
    };

    // PayTR validates user_ip and refuses private ranges, which is every developer's address on
    // localhost; the override exists for that and is ignored outside Development.
    private string ResolveUserIp(string? clientIp)
    {
        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(_options.DevUserIpOverride))
        {
            return _options.DevUserIpOverride.Trim();
        }

        return string.IsNullOrWhiteSpace(clientIp) ? "0.0.0.0" : clientIp.Trim();
    }

    private static CheckoutResponse ToCheckout(PaymentOrder order) =>
        new(order.Id, order.MerchantOid, PayTrClient.IframeUrlPrefix + order.IframeToken, order.TokenExpiresAt!.Value,
            order.Plan.ToString(), order.AmountMinor, order.Currency);
}
