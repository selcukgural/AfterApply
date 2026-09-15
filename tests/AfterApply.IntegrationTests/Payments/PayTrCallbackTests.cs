using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Infrastructure.Persistence;
using AfterApply.Infrastructure.Payments;
using AfterApply.Application.Payments;
using AfterApply.Domain.Payments;
using AfterApply.Domain.Pro;
using AfterApply.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>
/// Step 2 of the PayTR iFrame flow: the notification. These pin the answer discipline (when we
/// say OK and when we do not), idempotency on merchant_oid, and that the entitlement moves only
/// on a verified success.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PayTrCallbackTests(ApiHost<PaymentProfile> host) : IClassFixture<ApiHost<PaymentProfile>>, IAsyncLifetime
{
    private PaymentTestHost _host = null!;
    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _host = new PaymentTestHost(host);
        (_client, _userId) = await _host.RegisterAsync("callback@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<CheckoutResponse> StartAsync(string plan = "monthly", HttpClient? client = null)
    {
        var response = await (client ?? _client).PostAsJsonAsync("/api/payments/checkout",
            new StartCheckoutRequest(plan, "Ada Lovelace", "Somewhere 1", "+90 555 000 00 00", true), PaymentTestHost.JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions))!;
    }

    private Task<PaymentOrder> OrderAsync(Guid id) => _host.WithDbAsync(db => db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == id));

    private Task<List<PaymentNotification>> NotificationsAsync(string oid) =>
        _host.WithDbAsync(db => db.PaymentNotifications.AsNoTracking().Where(n => n.MerchantOid == oid).OrderBy(n => n.ReceivedAt).ToListAsync());

    [Fact]
    public async Task A_Verified_Success_Pays_The_Order_Grants_Pro_Queues_The_Receipt_And_Answers_OK()
    {
        var checkout = await StartAsync();

        var response = await _host.NotifyAsync(checkout.MerchantOid, "success", 29900);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        (await response.Content.ReadAsStringAsync()).ShouldBe("OK");

        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.TotalAmountMinor.ShouldBe(29900);
        order.PaymentType.ShouldBe("card");
        order.TestMode.ShouldBeTrue();
        order.AmountMismatch.ShouldBeFalse();
        order.EntitlementActiveUntilBefore.ShouldBe(PaymentTestHost.Start);
        order.EntitlementActiveUntilAfter.ShouldBe(PaymentTestHost.Start.AddMonths(1));

        var entitlement = await _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleAsync(e => e.UserId == _userId));
        entitlement.Source.ShouldBe(ProEntitlementSource.PayTr);
        entitlement.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        entitlement.IsActive(PaymentTestHost.Start).ShouldBeTrue();

        var notification = (await NotificationsAsync(checkout.MerchantOid)).Single();
        notification.Outcome.ShouldBe(PaymentNotificationOutcome.Applied);
        notification.HashValid.ShouldBeTrue();
        notification.OrderId.ShouldBe(checkout.OrderId);
        notification.RawForm.ShouldNotContain("\"hash\"");

        // The user's own view now says paid, with the new end date.
        var mine = await _client.GetFromJsonAsync<PaymentOrderResponse>($"/api/payments/orders/{checkout.OrderId}", PaymentTestHost.JsonOptions);
        mine!.Status.ShouldBe("Paid");
        mine.EntitlementActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        mine.CanRequestRefund.ShouldBeTrue();

        var plans = await _client.GetFromJsonAsync<PaymentPlansResponse>("/api/payments/plans", PaymentTestHost.JsonOptions);
        plans!.Entitlement.IsActive.ShouldBeTrue();

        // The receipt goes out as a background job, not inline with the callback — and it does go
        // out, once, to the buyer, once the job runs.
        _host.Emails.Receipts.ShouldBeEmpty();
        await _host.RunJobsAsync();
        var (to, _, _) = _host.Emails.Receipts.ShouldHaveSingleItem();
        to.ShouldBe("callback@example.com");
    }

    [Fact]
    public async Task A_Running_Entitlement_Is_Extended_From_Its_End_Not_From_Now()
    {
        var existingEnd = PaymentTestHost.Start.AddDays(10);
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.ProEntitlements.Add(ProEntitlement.Grant(_userId, existingEnd, ProEntitlementSource.Manual, PaymentTestHost.Start));
            await db.SaveChangesAsync();
        });
        var checkout = await StartAsync("yearly");

        (await _host.NotifyAsync(checkout.MerchantOid, "success", 299000)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var entitlement = await _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleAsync(e => e.UserId == _userId));
        entitlement.ActiveUntil.ShouldBe(existingEnd.AddYears(1));
        entitlement.Source.ShouldBe(ProEntitlementSource.PayTr);
        (await OrderAsync(checkout.OrderId)).EntitlementActiveUntilBefore.ShouldBe(existingEnd);
    }

    [Fact]
    public async Task A_Repeated_Success_Is_Acknowledged_Without_A_Second_Extension()
    {
        var checkout = await StartAsync();
        (await _host.NotifyAsync(checkout.MerchantOid, "success", 29900)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await _host.NotifyAsync(checkout.MerchantOid, "success", 29900);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.Content.ReadAsStringAsync()).ShouldBe("OK");
        var entitlement = await _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleAsync(e => e.UserId == _userId));
        entitlement.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        (await NotificationsAsync(checkout.MerchantOid)).Select(n => n.Outcome)
            .ShouldBe([PaymentNotificationOutcome.Applied, PaymentNotificationOutcome.Duplicate]);
    }

    [Fact]
    public async Task Simultaneous_Notifications_For_One_Order_Extend_Exactly_Once()
    {
        var checkout = await StartAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _host.NotifyAsync(checkout.MerchantOid, "success", 29900)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var entitlement = await _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleAsync(e => e.UserId == _userId));
        entitlement.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        var outcomes = (await NotificationsAsync(checkout.MerchantOid)).Select(n => n.Outcome).ToList();
        outcomes.Count(o => o == PaymentNotificationOutcome.Applied).ShouldBe(1);
        outcomes.Count.ShouldBe(6);
    }

    [Fact]
    public async Task A_Bad_Hash_Changes_Nothing_And_Is_Refused()
    {
        var checkout = await StartAsync();

        var response = await _host.NotifyAsync(checkout.MerchantOid, "success", 29900, hashOverride: "bm90LWEtcmVhbC1oYXNo");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe("PAYTR notification failed: bad hash");
        (await OrderAsync(checkout.OrderId)).Status.ShouldBe(PaymentOrderStatus.Pending);
        (await _host.WithDbAsync(db => db.ProEntitlements.AnyAsync(e => e.UserId == _userId))).ShouldBeFalse();
        var notification = (await NotificationsAsync(checkout.MerchantOid)).Single();
        notification.Outcome.ShouldBe(PaymentNotificationOutcome.BadHash);
        notification.HashValid.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Tampered_Amount_Fails_The_Hash()
    {
        var checkout = await StartAsync();
        var signedForOriginal = PayTrSignature.CallbackHash(checkout.MerchantOid, PaymentTestHost.MerchantSalt, "success", "29900", PaymentTestHost.MerchantKey);

        var response = await _host.NotifyAsync(checkout.MerchantOid, "success", 1, hashOverride: signedForOriginal);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await OrderAsync(checkout.OrderId)).Status.ShouldBe(PaymentOrderStatus.Pending);
    }

    [Fact]
    public async Task A_Missing_Field_Is_Malformed()
    {
        var response = await _host.AnonymousClient().PostAsync("/api/payments/paytr/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["merchant_oid"] = "abc", ["status"] = "success" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldBe("PAYTR notification failed: missing field");
        (await NotificationsAsync("abc")).Single().Outcome.ShouldBe(PaymentNotificationOutcome.Malformed);
    }

    [Fact]
    public async Task A_Json_Body_Is_Refused_Without_Being_Read_As_A_Notification()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync("/api/payments/paytr/callback", new { merchant_oid = "abc" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Failure_Records_The_Reason_And_Answers_OK()
    {
        var checkout = await StartAsync();

        var response = await _host.NotifyAsync(checkout.MerchantOid, "failed", 29900,
            new Dictionary<string, string> { ["failed_reason_code"] = "6", ["failed_reason_msg"] = "Müşteri ödeme yapmaktan vazgeçti ve ödeme sayfasından ayrıldı." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Failed);
        order.FailedReasonCode.ShouldBe(6);
        order.FailedReasonMsg.ShouldStartWith("Müşteri ödeme yapmaktan vazgeçti");
        (await _host.WithDbAsync(db => db.ProEntitlements.AnyAsync(e => e.UserId == _userId))).ShouldBeFalse();

        var mine = await _client.GetFromJsonAsync<PaymentOrderResponse>($"/api/payments/orders/{checkout.OrderId}", PaymentTestHost.JsonOptions);
        mine!.Status.ShouldBe("Failed");
        mine.FailedReasonCode.ShouldBe(6);
        mine.CanRequestRefund.ShouldBeFalse();

        // A repeat of the same failure is a duplicate, still OK.
        (await _host.NotifyAsync(checkout.MerchantOid, "failed", 29900)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await NotificationsAsync(checkout.MerchantOid)).Last().Outcome.ShouldBe(PaymentNotificationOutcome.Duplicate);
    }

    [Fact]
    public async Task An_Unknown_Order_With_A_Valid_Hash_Is_Answered_Non_OK_So_PayTR_Keeps_Trying()
    {
        var response = await _host.NotifyAsync("ffffffffffffffffffffffffffffffff", "success", 29900);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe("PAYTR notification failed: unknown order");
        (await NotificationsAsync("ffffffffffffffffffffffffffffffff")).Single().Outcome.ShouldBe(PaymentNotificationOutcome.UnknownOrder);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("cancelled")]
    [InlineData("failed")]
    public async Task Success_After_The_Order_Was_Closed_Is_Still_Honoured(string closedBy)
    {
        var checkout = await StartAsync();
        switch (closedBy)
        {
            case "expired":
                _host.Clock.Advance(TimeSpan.FromHours(2));
                await _host.WithScopeAsync(sp => sp.GetRequiredService<IPaymentMaintenanceService>().ExpirePendingOrdersAsync(CancellationToken.None));
                (await OrderAsync(checkout.OrderId)).Status.ShouldBe(PaymentOrderStatus.Expired);
                break;
            case "cancelled":
                (await _client.PostAsync($"/api/payments/orders/{checkout.OrderId}/cancel", null)).EnsureSuccessStatusCode();
                break;
            case "failed":
                (await _host.NotifyAsync(checkout.MerchantOid, "failed", 29900, new Dictionary<string, string> { ["failed_reason_code"] = "2" })).EnsureSuccessStatusCode();
                break;
        }

        var response = await _host.NotifyAsync(checkout.MerchantOid, "success", 29900);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await OrderAsync(checkout.OrderId)).Status.ShouldBe(PaymentOrderStatus.Paid);
        (await _host.WithDbAsync(db => db.ProEntitlements.AnyAsync(e => e.UserId == _userId && e.RevokedAt == null))).ShouldBeTrue();
        (await NotificationsAsync(checkout.MerchantOid)).Last().Outcome.ShouldBe(PaymentNotificationOutcome.LateApplied);
    }

    [Fact]
    public async Task A_Different_Charged_Amount_Is_Applied_And_Flagged()
    {
        var checkout = await StartAsync();

        (await _host.NotifyAsync(checkout.MerchantOid, "success", 31000)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.AmountMismatch.ShouldBeTrue();
        order.TotalAmountMinor.ShouldBe(31000);
    }

    [Fact]
    public async Task The_Notification_Is_Audited_Like_Any_Other_Write_Without_A_User()
    {
        var checkout = await StartAsync();
        await _host.NotifyAsync(checkout.MerchantOid, "success", 29900);

        var audit = await _host.WithDbAsync(db => db.RequestAudits.AsNoTracking()
            .Where(a => a.Path == "/api/payments/paytr/callback").OrderByDescending(a => a.At).FirstAsync());

        audit.UserId.ShouldBeNull();
        audit.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Success_For_A_Deleted_Account_Is_Recorded_Without_An_Entitlement()
    {
        var (client, userId) = await _host.RegisterAsync("leaving@example.com");
        var checkout = await StartAsync(client: client);
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == userId));
            await db.SaveChangesAsync();
        });

        var response = await _host.NotifyAsync(checkout.MerchantOid, "success", 29900);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.UserId.ShouldBeNull();
        order.BillingName.ShouldBe("Ada Lovelace");
        (await _host.WithDbAsync(db => db.ProEntitlements.AnyAsync(e => e.UserId == userId))).ShouldBeFalse();
    }
}
