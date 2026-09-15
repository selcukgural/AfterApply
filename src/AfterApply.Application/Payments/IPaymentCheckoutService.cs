using AfterApply.Application.Payments.Contracts;

namespace AfterApply.Application.Payments;

/// <summary>Step 1 of the PayTR iFrame flow and the user's view of their orders.</summary>
public interface IPaymentCheckoutService
{
    Task<PaymentPlansResponse> GetPlansAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Creates (or resumes) a pending order and fetches PayTR's iframe token for it.
    /// <paramref name="clientIp"/> is the customer's address as PayTR requires it;
    /// <paramref name="locale"/> picks the payment page language and the return URLs.</summary>
    Task<CheckoutResponse> StartAsync(Guid userId, StartCheckoutRequest request, string? clientIp, string locale, CancellationToken cancellationToken);

    Task<PaymentOrderResponse?> GetOrderAsync(Guid userId, Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentOrderResponse>> ListOrdersAsync(Guid userId, int take, CancellationToken cancellationToken);

    /// <summary>The user closed the payment window before paying. False when the order is not
    /// theirs or does not exist; throws when it is no longer pending.</summary>
    Task<bool> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken);
}
