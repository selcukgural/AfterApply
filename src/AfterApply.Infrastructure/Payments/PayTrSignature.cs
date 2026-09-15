using System.Security.Cryptography;
using System.Text;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// The three HMAC-SHA256 signatures of the PayTR iFrame and refund APIs, exactly as their sample
/// code concatenates the fields. Kept as pure functions so the byte-for-byte order can be pinned
/// by unit tests — a wrong order here is "paytr_token gönderilmedi veya geçersiz" in production
/// and nothing else. Every result is base64 of the raw HMAC, the key is the merchant key.
/// </summary>
public static class PayTrSignature
{
    /// <summary>Step 1, <c>paytr_token</c> of the get-token request.</summary>
    public static string TokenHash(
        string merchantId,
        string userIp,
        string merchantOid,
        string email,
        long paymentAmount,
        string userBasketBase64,
        int noInstallment,
        int maxInstallment,
        string currency,
        int testMode,
        string merchantSalt,
        string merchantKey)
    {
        var payload = string.Concat(merchantId, userIp, merchantOid, email, paymentAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            userBasketBase64, noInstallment.ToString(), maxInstallment.ToString(), currency, testMode.ToString(), merchantSalt);
        return Hmac(payload, merchantKey);
    }

    /// <summary>Step 2, the <c>hash</c> PayTR sends with a notification.</summary>
    public static string CallbackHash(string merchantOid, string merchantSalt, string status, string totalAmount, string merchantKey) =>
        Hmac(string.Concat(merchantOid, merchantSalt, status, totalAmount), merchantKey);

    /// <summary>Constant-time comparison of the notification's hash with ours. Both sides are
    /// base64 text; comparing their bytes as text is fine because equal hashes encode identically.</summary>
    public static bool VerifyCallback(string merchantOid, string merchantSalt, string status, string totalAmount, string merchantKey, string presentedHash)
    {
        var expected = Encoding.UTF8.GetBytes(CallbackHash(merchantOid, merchantSalt, status, totalAmount, merchantKey));
        var presented = Encoding.UTF8.GetBytes(presentedHash);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    /// <summary>Refund API, <c>paytr_token</c>. Note the amount here is a decimal string
    /// ("299.00"), not the ×100 integer of step 1 — the same text must go on the wire.</summary>
    public static string RefundHash(string merchantId, string merchantOid, string returnAmount, string merchantSalt, string merchantKey) =>
        Hmac(string.Concat(merchantId, merchantOid, returnAmount, merchantSalt), merchantKey);

    private static string Hmac(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
