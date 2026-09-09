namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Makes a caller-supplied string safe to put in a log line.
///
/// The sign-in endpoints log the redirect URI they rejected, and that value comes straight out of
/// the request body. A raw CR/LF inside it would let anyone append a convincing second line to our
/// logs — a forged "sign-in succeeded" sitting directly under their own failure (CodeQL
/// cs/log-forging). This is not theoretical for a URI: <c>Uri.TryCreate</c> accepts an absolute URL
/// with an embedded newline and hands it back with the newline intact, so the request passes
/// validation and reaches the logger unchanged.
/// </summary>
public static class SafeLogValue
{
    /// <summary>The value with CR and LF removed and nothing else touched — what makes the log line
    /// worth having is seeing the exact value that was rejected, which is nearly always a
    /// deployment's own origin being wrong rather than an attack. Length is already capped upstream
    /// by the request validators.</summary>
    public static string SingleLine(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
}
