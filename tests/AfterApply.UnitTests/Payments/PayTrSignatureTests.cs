using System.Security.Cryptography;
using System.Text;
using AfterApply.Infrastructure.Payments;
using Shouldly;

namespace AfterApply.UnitTests.Payments;

/// <summary>Pins the concatenation order of PayTR's three signatures against an independent
/// computation of the documented formula — a wrong order is invisible until production.</summary>
public sealed class PayTrSignatureTests
{
    private const string MerchantId = "123456";
    private const string Key = "YYYYYYYYYYYYYY";
    private const string Salt = "ZZZZZZZZZZZZZZ";

    [Fact]
    public void Token_hash_concatenates_the_documented_fields_in_order()
    {
        var basket = PayTrBasket.Encode("e-kariyerim Pro — 1 ay", 29900);
        var expected = Hmac(string.Concat(MerchantId, "203.0.113.7", "abc123", "ada@example.com", "29900", basket, "1", "0", "TL", "1", Salt), Key);

        var actual = PayTrSignature.TokenHash(MerchantId, "203.0.113.7", "abc123", "ada@example.com", 29900, basket, 1, 0, "TL", 1, Salt, Key);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Callback_hash_is_oid_salt_status_total()
    {
        var expected = Hmac(string.Concat("abc123", Salt, "success", "29900"), Key);

        PayTrSignature.CallbackHash("abc123", Salt, "success", "29900", Key).ShouldBe(expected);
        PayTrSignature.VerifyCallback("abc123", Salt, "success", "29900", Key, expected).ShouldBeTrue();
    }

    [Theory]
    [InlineData("failed", "29900")]
    [InlineData("success", "29901")]
    [InlineData("success", "")]
    public void Callback_verification_fails_when_any_signed_field_differs(string status, string total)
    {
        var signed = PayTrSignature.CallbackHash("abc123", Salt, "success", "29900", Key);

        PayTrSignature.VerifyCallback("abc123", Salt, status, total, Key, signed).ShouldBeFalse();
    }

    [Fact]
    public void Callback_verification_fails_for_a_hash_of_a_different_length_or_garbage()
    {
        PayTrSignature.VerifyCallback("abc123", Salt, "success", "29900", Key, "not-a-hash").ShouldBeFalse();
        PayTrSignature.VerifyCallback("abc123", Salt, "success", "29900", Key, string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void Refund_hash_uses_the_decimal_amount_text()
    {
        var returnAmount = PayTrMoney.ToDecimalString(29900);
        returnAmount.ShouldBe("299.00");

        PayTrSignature.RefundHash(MerchantId, "abc123", returnAmount, Salt, Key)
            .ShouldBe(Hmac(string.Concat(MerchantId, "abc123", "299.00", Salt), Key));
    }

    private static string Hmac(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}

public sealed class PayTrMoneyAndBasketTests
{
    [Theory]
    [InlineData(29900, "299.00")]
    [InlineData(5, "0.05")]
    [InlineData(123456789, "1234567.89")]
    public void Decimal_text_uses_a_period_and_two_places_whatever_the_culture(long minor, string expected)
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            PayTrMoney.ToDecimalString(minor).ShouldBe(expected);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("29900", 29900L)]
    [InlineData("0", 0L)]
    [InlineData("299.00", null)]
    [InlineData("-1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Notification_amounts_are_plain_integers_only(string? text, long? expected)
    {
        PayTrMoney.ParseMinor(text).ShouldBe(expected);
    }

    [Fact]
    public void Basket_is_base64_of_a_single_row_array()
    {
        var encoded = PayTrBasket.Encode("Pro", 29900);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

        json.ShouldBe("""[["Pro","299.00",1]]""");
    }
}
