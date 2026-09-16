using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Domain.Payments;
using AfterApply.Infrastructure.Payments;
using AfterApply.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>Step 1 of the PayTR iFrame flow: the pending order and the token request.</summary>
[Collection(IntegrationTestCollection.Name)]
public class PayTrCheckoutTests(ApiHost<PaymentProfile> host) : IClassFixture<ApiHost<PaymentProfile>>, IAsyncLifetime
{
    private PaymentTestHost _host = null!;
    private HttpClient _client = null!;
    private Guid _userId;

    private static StartCheckoutRequest Monthly() =>
        new("monthly", "Ada Lovelace", "Somewhere 1, İstanbul", "+90 555 000 00 00", true);

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _host = new PaymentTestHost(host);
        (_client, _userId) = await _host.RegisterAsync("checkout@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Plans_Lists_Prices_And_The_Callers_Entitlement()
    {
        var plans = await _client.GetFromJsonAsync<PaymentPlansResponse>("/api/payments/plans", PaymentTestHost.JsonOptions);

        plans!.Currency.ShouldBe("TL");
        plans.Plans.Select(p => (p.Plan, p.AmountMinor, p.Months)).ShouldBe([("Monthly", PaymentTestHost.MonthlyPrice, 1), ("Yearly", PaymentTestHost.YearlyPrice, 12)]);
        plans.Entitlement.IsActive.ShouldBeFalse();
        plans.TermsVersion.ShouldBe("2026-09");
    }

    [Fact]
    public async Task Checkout_Writes_A_Pending_Order_Signs_The_Token_Request_And_Returns_The_Frame_Url()
    {
        var response = await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        checkout!.IframeUrl.ShouldBe("https://www.paytr.com/odeme/guvenli/stub-iframe-token");
        checkout.AmountMinor.ShouldBe(PaymentTestHost.MonthlyPrice);
        checkout.Plan.ShouldBe("Monthly");
        checkout.MerchantOid.ShouldBe(checkout.OrderId.ToString("N"));
        checkout.ExpiresAt.ShouldBe(PaymentTestHost.Start.AddMinutes(30));

        var order = await _host.WithDbAsync(db => db.PaymentOrders.SingleAsync(o => o.Id == checkout.OrderId));
        order.Status.ShouldBe(PaymentOrderStatus.Pending);
        order.UserId.ShouldBe(_userId);
        order.Email.ShouldBe("checkout@example.com");
        order.BillingName.ShouldBe("Ada Lovelace");
        order.IframeToken.ShouldBe("stub-iframe-token");
        order.TermsVersion.ShouldBe("2026-09");
        order.Locale.ShouldBe("tr");

        // What PayTR was sent, and that the signature matches what its sample code computes.
        var (path, form) = _host.PayTr.Requests.Single();
        path.ShouldEndWith("/odeme/api/get-token");
        form["merchant_id"].ShouldBe(PaymentTestHost.MerchantId);
        form["user_ip"].ShouldBe(PaymentTestHost.ClientIp);
        form["merchant_oid"].ShouldBe(order.MerchantOid);
        form["email"].ShouldBe("checkout@example.com");
        form["payment_amount"].ShouldBe("29900");
        form["no_installment"].ShouldBe("1");
        form["max_installment"].ShouldBe("0");
        form["test_mode"].ShouldBe("1");
        form["currency"].ShouldBe("TL");
        form["lang"].ShouldBe("tr");
        form["timeout_limit"].ShouldBe("30");
        form["merchant_ok_url"].ShouldBe($"https://www.ekariyerim.com/tr/pro/return/{order.Id}?outcome=ok");
        form["merchant_fail_url"].ShouldBe($"https://www.ekariyerim.com/tr/pro/return/{order.Id}?outcome=fail");
        form["user_name"].ShouldBe("Ada Lovelace");
        form["user_phone"].ShouldBe("+90 555 000 00 00");
        form["paytr_token"].ShouldBe(PayTrSignature.TokenHash(PaymentTestHost.MerchantId, PaymentTestHost.ClientIp, order.MerchantOid,
            "checkout@example.com", 29900, form["user_basket"], 1, 0, "TL", 1, PaymentTestHost.MerchantSalt, PaymentTestHost.MerchantKey));
        // The secrets themselves never travel.
        form.Values.ShouldNotContain(PaymentTestHost.MerchantKey);
        form.Values.ShouldNotContain(PaymentTestHost.MerchantSalt);
    }

    [Fact]
    public async Task English_Locale_Sets_The_Payment_Page_Language_And_Return_Urls()
    {
        var (english, _) = await _host.RegisterAsync("checkout.en@example.com", locale: "en");

        var checkout = await (await english.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        var form = _host.PayTr.Requests.Single(r => r.Form["merchant_oid"] == checkout!.MerchantOid).Form;
        form["lang"].ShouldBe("en");
        form["merchant_ok_url"].ShouldStartWith("https://www.ekariyerim.com/en/pro/return/");
    }

    [Fact]
    public async Task Reloading_The_Checkout_Resumes_The_Pending_Order_Instead_Of_Making_Another()
    {
        var first = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        var second = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        second!.OrderId.ShouldBe(first!.OrderId);
        _host.PayTr.Requests.Count.ShouldBe(1);

        // A different plan is a different order.
        var yearly = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly() with { Plan = "yearly" }, PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        yearly!.OrderId.ShouldNotBe(first.OrderId);
        yearly.AmountMinor.ShouldBe(PaymentTestHost.YearlyPrice);
    }

    [Fact]
    public async Task A_Pending_Order_Near_Its_Expiry_Is_Not_Resumed()
    {
        var first = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        _host.Clock.Advance(TimeSpan.FromMinutes(27));

        var second = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        second!.OrderId.ShouldNotBe(first!.OrderId);
    }

    [Theory]
    [InlineData("tr", "Ödeme sağlayıcısına ulaşılamadı")]
    [InlineData("en", "The payment provider could not be reached")]
    public async Task A_Rejected_Token_Request_Fails_The_Order_And_Tells_The_User_In_Their_Language(string locale, string expected)
    {
        _host.PayTr.TokenResponse = _ => StubPayTrHandler.Json("""{"status":"failed","reason":"Zorunlu alan degeri gecersiz: user_ip"}""");
        var (client, userId) = await _host.RegisterAsync($"rejected.{locale}@example.com", locale);

        var response = await client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsLike>(PaymentTestHost.JsonOptions);
        problem!.Detail.ShouldContain(expected);
        // PayTR's reason stays in the log and the row, not in the answer.
        problem.Detail.ShouldNotContain("user_ip");

        var order = await _host.WithDbAsync(db => db.PaymentOrders.SingleAsync(o => o.UserId == userId));
        order.Status.ShouldBe(PaymentOrderStatus.Failed);
        order.FailedReasonCode.ShouldBe(PaymentOrder.ProviderRejectedReasonCode);
        order.FailedReasonMsg.ShouldBe("Zorunlu alan degeri gecersiz: user_ip");
    }

    [Fact]
    public async Task A_Provider_Outage_Fails_The_Order_The_Same_Way()
    {
        _host.PayTr.TokenResponse = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);

        var response = await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var order = await _host.WithDbAsync(db => db.PaymentOrders.SingleAsync(o => o.UserId == _userId));
        order.Status.ShouldBe(PaymentOrderStatus.Failed);
        order.FailedReasonMsg.ShouldBe("HTTP 502");
    }

    [Fact]
    public async Task Validation_Refuses_Missing_Terms_And_Bad_Billing_Fields()
    {
        var response = await _client.PostAsJsonAsync("/api/payments/checkout",
            new StartCheckoutRequest("monthly", "", "addr", "asdf", false), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("billingName");
        body.ShouldContain("billingPhone");
        body.ShouldContain("acceptTerms");
        _host.PayTr.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelling_Closes_A_Pending_Order_And_Only_That()
    {
        var checkout = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        (await _client.PostAsync($"/api/payments/orders/{checkout!.OrderId}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.PostAsync($"/api/payments/orders/{checkout.OrderId}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var order = await _client.GetFromJsonAsync<PaymentOrderResponse>($"/api/payments/orders/{checkout.OrderId}", PaymentTestHost.JsonOptions);
        order!.Status.ShouldBe("Cancelled");
    }

    [Fact]
    public async Task Orders_Are_Scoped_To_Their_Owner()
    {
        var checkout = await (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        var (other, _) = await _host.RegisterAsync("other@example.com");

        (await other.GetAsync($"/api/payments/orders/{checkout!.OrderId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/payments/orders/{checkout.OrderId}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.GetFromJsonAsync<List<PaymentOrderResponse>>("/api/payments/orders", PaymentTestHost.JsonOptions))!.ShouldBeEmpty();
        (await _client.GetFromJsonAsync<List<PaymentOrderResponse>>("/api/payments/orders", PaymentTestHost.JsonOptions))!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Billing_Defaults_Echo_The_Newest_Order_And_Only_To_Its_Owner()
    {
        (await _client.GetFromJsonAsync<BillingDefaultsResponse>("/api/payments/billing-defaults", PaymentTestHost.JsonOptions))!
            .ShouldBe(new BillingDefaultsResponse(null, null, null));

        (await _client.PostAsJsonAsync("/api/payments/checkout", Monthly(), PaymentTestHost.JsonOptions)).EnsureSuccessStatusCode();
        var newer = Monthly() with { BillingAddress = "Elsewhere 2, Ankara", BillingPhone = "+90 555 111 11 11" };
        (await _client.PostAsJsonAsync("/api/payments/checkout", newer with { Plan = "yearly" }, PaymentTestHost.JsonOptions)).EnsureSuccessStatusCode();

        var defaults = await _client.GetFromJsonAsync<BillingDefaultsResponse>("/api/payments/billing-defaults", PaymentTestHost.JsonOptions);
        defaults.ShouldBe(new BillingDefaultsResponse("Ada Lovelace", "Elsewhere 2, Ankara", "+90 555 111 11 11"));

        var (other, _) = await _host.RegisterAsync("other@example.com");
        (await other.GetFromJsonAsync<BillingDefaultsResponse>("/api/payments/billing-defaults", PaymentTestHost.JsonOptions))!
            .ShouldBe(new BillingDefaultsResponse(null, null, null));
    }

    [Fact]
    public async Task Anonymous_Callers_Get_401()
    {
        (await _host.AnonymousClient().GetAsync("/api/payments/plans")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _host.AnonymousClient().GetAsync("/api/payments/billing-defaults")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record ProblemDetailsLike(string? Title, string? Detail, int? Status);
}

/// <summary>With the switch off (or the secrets missing) nothing of the checkout is observable —
/// except the notification endpoint, which only needs the secrets.</summary>
[Collection(IntegrationTestCollection.Name)]
public class PayTrFlagOffTests(ApiHost<PayTrFlagOffProfile> host) : IClassFixture<ApiHost<PayTrFlagOffProfile>>, IAsyncLifetime
{
    private PaymentTestHost _host = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _host = new PaymentTestHost(host);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Checkout_Routes_404_And_Config_Says_Disabled_But_The_Callback_Still_Answers()
    {
        var (client, _) = await _host.RegisterAsync("flagoff@example.com");

        (await client.GetAsync("/api/payments/plans")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/payments/checkout",
            new StartCheckoutRequest("monthly", "a", "b", "555", true), PaymentTestHost.JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var config = await _host.AnonymousClient().GetFromJsonAsync<ConfigLike>("/api/config", PaymentTestHost.JsonOptions);
        config!.Payments!.Enabled.ShouldBeFalse();

        // Valid hash, unknown order: the endpoint is alive and verifies, it just has nothing to
        // apply — recorded and answered OK (a bad hash would still be a 400, proving it checks).
        var response = await _host.NotifyAsync("0123456789abcdef0123456789abcdef", "success", 29900);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _host.NotifyAsync("0123456789abcdef0123456789abcdef", "success", 29900, hashOverride: "bad")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record ConfigLike(PaymentsLike? Payments);

    private sealed record PaymentsLike(bool Enabled);
}
