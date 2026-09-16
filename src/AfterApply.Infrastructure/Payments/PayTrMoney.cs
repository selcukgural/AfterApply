using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AfterApply.Infrastructure.Payments;

public static class PayTrMoney
{
    /// <summary>Kuruş to PayTR's decimal text: 29900 → "299.00". Invariant culture so the
    /// separator is a period whatever the server's locale — the refund hash is computed over this text.</summary>
    public static string ToDecimalString(long amountMinor) =>
        (amountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>PayTR's "34.56 => 3456" convention, parsed back from a notification field.
    /// Returns null for anything that is not a plain non-negative integer.</summary>
    public static long? ParseMinor(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ? minor : null;
}

public static class PayTrBasket
{
    /// <summary>The <c>user_basket</c> field: base64 of a JSON array of [name, unit price, quantity]
    /// rows, as PayTR's samples build it. One row — one plan period.</summary>
    public static string Encode(string productName, long amountMinor)
    {
        var rows = new object[][] { [productName, PayTrMoney.ToDecimalString(amountMinor), 1] };
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    // PayTR decodes the JSON itself; escaping non-ASCII as \uXXXX (the default) is what its
    // samples produce in PHP/.NET, so the Turkish plan name survives either way.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
}
