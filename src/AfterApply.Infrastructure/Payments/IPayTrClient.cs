namespace AfterApply.Infrastructure.Payments;

/// <summary>The two server-side calls of the PayTR integration. Both are typed-client HTTP; the
/// notification (step 2) is inbound and lives in the callback service instead.</summary>
public interface IPayTrClient
{
    Task<PayTrTokenResult> GetIframeTokenAsync(PayTrTokenRequest request, CancellationToken cancellationToken);

    Task<PayTrRefundResult> RefundAsync(string merchantOid, long amountMinor, string referenceNo, CancellationToken cancellationToken);
}

public sealed record PayTrTokenRequest(
    string UserIp,
    string MerchantOid,
    string Email,
    long AmountMinor,
    string ProductName,
    string UserName,
    string UserAddress,
    string UserPhone,
    string OkUrl,
    string FailUrl,
    string Lang);

/// <summary>Outcome of the get-token call. <see cref="Rejected"/> is PayTR's own "failed" with a
/// reason (a field it did not like — an integration bug); <see cref="Unavailable"/> is a network
/// or server problem that a retry may cure. Neither reason is for the user's eyes.</summary>
public abstract record PayTrTokenResult
{
    public sealed record Success(string Token) : PayTrTokenResult;

    public sealed record Rejected(string Reason) : PayTrTokenResult;

    public sealed record Unavailable(string Reason) : PayTrTokenResult;
}

public abstract record PayTrRefundResult
{
    /// <param name="ReturnAmount">The decimal text PayTR echoed back ("299.00").</param>
    public sealed record Success(string ReturnAmount, string? ReferenceNo, bool IsTest) : PayTrRefundResult;

    /// <summary>PayTR answered and said no: <c>status=error</c> with an error number and message,
    /// or <c>status=failed</c> (no such transaction). Nothing was refunded.</summary>
    public sealed record Rejected(string? ErrorNo, string Message) : PayTrRefundResult;

    /// <summary>No usable answer. The refund may or may not have gone through — the caller must
    /// not assume either; the admin checks the merchant panel.</summary>
    public sealed record Unavailable(string Reason) : PayTrRefundResult;
}
