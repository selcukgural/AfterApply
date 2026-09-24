using System.Text;
using AfterApply.Application.Mailing;
using AfterApply.Application.Payments;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Mailing;

/// <summary>See <see cref="IAccountEmailJobs"/>. The reset token is minted here rather than at the
/// request, so it never sits in the job's stored arguments; a retry mints a new one, which only
/// makes an earlier email's link stale.</summary>
internal sealed class AccountEmailJobs(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions) : IAccountEmailJobs
{
    public async Task SendPasswordResetAsync(Guid userId, string locale, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user?.Email is null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetLink = $"{appOptions.Value.WebBaseUrl}/{locale}/reset-password" +
                        $"?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(encodedToken)}";

        await emailSender.SendPasswordResetEmailAsync(user.Email, resetLink, locale, cancellationToken);
    }

    public async Task SendPasswordChangedAsync(Guid userId, string locale, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user?.Email is null)
        {
            return;
        }

        await emailSender.SendPasswordChangedEmailAsync(user.Email, locale, cancellationToken);
    }
}

/// <summary>See <see cref="IPaymentEmailJobs"/>. Everything the emails say is read back from the
/// order when the job runs; only the amount and date that belong to the one event are passed in.</summary>
internal sealed class PaymentEmailJobs(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions) : IPaymentEmailJobs
{
    public async Task SendReceiptAsync(Guid orderId, DateTimeOffset activeUntil, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || string.IsNullOrWhiteSpace(order.Email))
        {
            return;
        }

        var locale = PaymentFormatting.NormalizeLocale(order.Locale);
        var receipt = new PaymentReceipt(
            PaymentFormatting.PlanName(order.Plan, locale),
            PaymentFormatting.Amount(order.PaidAmountMinor, locale),
            PaymentFormatting.Date(activeUntil, locale),
            ProLink(locale));
        await emailSender.SendPaymentReceivedEmailAsync(order.Email, locale, receipt, cancellationToken);
    }

    public async Task SendRefundCompletedAsync(Guid orderId, long amountMinor, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || string.IsNullOrWhiteSpace(order.Email))
        {
            return;
        }

        var locale = PaymentFormatting.NormalizeLocale(order.Locale);
        await emailSender.SendRefundCompletedEmailAsync(order.Email, locale, PaymentFormatting.Amount(amountMinor, locale), cancellationToken);
    }

    public async Task SendRefundRejectedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || string.IsNullOrWhiteSpace(order.Email) || order.RefundRejectionNote is null)
        {
            return;
        }

        var locale = PaymentFormatting.NormalizeLocale(order.Locale);
        await emailSender.SendRefundRejectedEmailAsync(order.Email, locale, order.RefundRejectionNote, cancellationToken);
    }

    public async Task SendProExpiringAsync(Guid userId, DateTimeOffset activeUntil, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            return;
        }

        var locale = PaymentFormatting.NormalizeLocale(user.PreferredLanguage);
        await emailSender.SendProExpiringEmailAsync(user.Email, locale, PaymentFormatting.Date(activeUntil, locale), ProLink(locale),
            cancellationToken);
    }

    private string ProLink(string locale) => $"{appOptions.Value.WebBaseUrl.TrimEnd('/')}/{locale}/pro";
}
