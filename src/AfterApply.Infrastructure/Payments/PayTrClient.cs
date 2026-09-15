using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// Talks to PayTR: step 1 of the iFrame API (get an iframe token) and the refund API. Form-encoded
/// POSTs signed with <see cref="PayTrSignature"/>. Logs carry the merchant_oid and PayTR's reason
/// text, never the key, the salt, or a computed hash. No automatic retry: a token request is
/// cheap to repeat from the UI, and a refund must never be repeated by a machine.
/// </summary>
internal sealed class PayTrClient(HttpClient httpClient, IOptions<PayTrOptions> options, ILogger<PayTrClient> logger) : IPayTrClient
{
    internal const string TokenPath = "odeme/api/get-token";
    internal const string RefundPath = "odeme/iade";
    internal const string IframeUrlPrefix = "https://www.paytr.com/odeme/guvenli/";

    private readonly PayTrOptions _options = options.Value;

    public async Task<PayTrTokenResult> GetIframeTokenAsync(PayTrTokenRequest request, CancellationToken cancellationToken)
    {
        RequireConfigured();

        const int noInstallment = 1;   // a prepaid digital service: single charge, no instalment table
        const int maxInstallment = 0;
        var testMode = _options.TestMode ? 1 : 0;
        var basket = PayTrBasket.Encode(request.ProductName, request.AmountMinor);
        var token = PayTrSignature.TokenHash(_options.MerchantId!, request.UserIp, request.MerchantOid, request.Email,
            request.AmountMinor, basket, noInstallment, maxInstallment, _options.Currency, testMode,
            _options.MerchantSalt!, _options.MerchantKey!);

        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = _options.MerchantId!,
            ["user_ip"] = request.UserIp,
            ["merchant_oid"] = request.MerchantOid,
            ["email"] = request.Email,
            ["payment_amount"] = request.AmountMinor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["user_basket"] = basket,
            ["paytr_token"] = token,
            ["debug_on"] = _options.DebugOn ? "1" : "0",
            ["test_mode"] = testMode.ToString(),
            ["no_installment"] = noInstallment.ToString(),
            ["max_installment"] = maxInstallment.ToString(),
            ["user_name"] = request.UserName,
            ["user_address"] = request.UserAddress,
            ["user_phone"] = request.UserPhone,
            ["merchant_ok_url"] = request.OkUrl,
            ["merchant_fail_url"] = request.FailUrl,
            ["timeout_limit"] = _options.TimeoutLimitMinutes.ToString(),
            ["currency"] = _options.Currency,
            ["lang"] = request.Lang
        };

        TokenResponse? body;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(TokenPath, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayTR get-token answered HTTP {Status} for order {MerchantOid}", (int)response.StatusCode, request.MerchantOid);
                return new PayTrTokenResult.Unavailable($"HTTP {(int)response.StatusCode}");
            }

            body = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "PayTR get-token unreachable or unreadable for order {MerchantOid}", request.MerchantOid);
            return new PayTrTokenResult.Unavailable(ex.GetType().Name);
        }

        if (body is { Status: "success", Token: { Length: > 0 } iframeToken })
        {
            return new PayTrTokenResult.Success(iframeToken);
        }

        var reason = body?.Reason ?? "(no reason)";
        logger.LogError("PayTR get-token rejected order {MerchantOid}: {Reason}", request.MerchantOid, reason);
        return new PayTrTokenResult.Rejected(reason);
    }

    public async Task<PayTrRefundResult> RefundAsync(string merchantOid, long amountMinor, string referenceNo, CancellationToken cancellationToken)
    {
        RequireConfigured();

        var returnAmount = PayTrMoney.ToDecimalString(amountMinor);
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = _options.MerchantId!,
            ["merchant_oid"] = merchantOid,
            ["return_amount"] = returnAmount,
            ["reference_no"] = referenceNo,
            ["paytr_token"] = PayTrSignature.RefundHash(_options.MerchantId!, merchantOid, returnAmount, _options.MerchantSalt!, _options.MerchantKey!)
        };

        RefundResponse? body;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(RefundPath, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayTR refund answered HTTP {Status} for order {MerchantOid}", (int)response.StatusCode, merchantOid);
                return new PayTrRefundResult.Unavailable($"HTTP {(int)response.StatusCode}");
            }

            body = await response.Content.ReadFromJsonAsync<RefundResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "PayTR refund unreachable or unreadable for order {MerchantOid}", merchantOid);
            return new PayTrRefundResult.Unavailable(ex.GetType().Name);
        }

        if (body is { Status: "success" })
        {
            return new PayTrRefundResult.Success(body.ReturnAmount ?? returnAmount, body.ReferenceNo, body.IsTest == 1);
        }

        var message = body?.ErrorMessage ?? body?.Reason ?? body?.Status ?? "(empty response)";
        logger.LogWarning("PayTR refund refused for order {MerchantOid}: {ErrorNo} {Message}", merchantOid, body?.ErrorNo, message);
        return new PayTrRefundResult.Rejected(body?.ErrorNo, message);
    }

    private void RequireConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "PayTr:MerchantId / MerchantKey / MerchantSalt are not configured. For local dev run 'dotnet user-secrets set \"PayTr:MerchantId\" ...' (see README).");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record TokenResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("reason")] string? Reason);

    private sealed record RefundResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("is_test")] int? IsTest,
        [property: JsonPropertyName("merchant_oid")] string? MerchantOid,
        [property: JsonPropertyName("return_amount")] string? ReturnAmount,
        [property: JsonPropertyName("reference_no")] string? ReferenceNo,
        [property: JsonPropertyName("err_no")] string? ErrorNo,
        [property: JsonPropertyName("err_msg")] string? ErrorMessage,
        [property: JsonPropertyName("reason")] string? Reason);
}
