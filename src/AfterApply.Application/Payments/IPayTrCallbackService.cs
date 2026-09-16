namespace AfterApply.Application.Payments;

/// <summary>Step 2 of the PayTR iFrame flow: the server-to-server notification. The one place a
/// payment becomes real. Idempotent on merchant_oid; answers "OK" only for notifications that
/// were applied or were repeats of applied ones, anything else non-OK so PayTR retries.</summary>
public interface IPayTrCallbackService
{
    Task<PayTrCallbackResult> HandleAsync(IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken);
}

/// <param name="StatusCode">HTTP status for PayTR; 200 with body "OK" is the only acknowledgement it accepts.</param>
public sealed record PayTrCallbackResult(int StatusCode, string Body)
{
    public static readonly PayTrCallbackResult Ok = new(200, "OK");

    public static PayTrCallbackResult Reject(int statusCode, string reason) => new(statusCode, $"PAYTR notification failed: {reason}");
}
