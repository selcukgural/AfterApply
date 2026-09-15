using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Payments;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Infrastructure.Payments;
using AfterApply.Domain.Payments;
using AfterApply.Domain.Pro;
using AfterApply.Infrastructure.Persistence;
using AfterApply.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>Refunds through our system (never the merchant panel) and the admin's payments panel API.</summary>
[Collection(IntegrationTestCollection.Name)]
public class PaymentRefundAndAdminTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private PaymentTestHost _host = null!;
    private HttpClient _user = null!;
    private HttpClient _admin = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _host = await PaymentTestHost.CreateAsync(shared, nameof(PaymentRefundAndAdminTests));
        (_user, _userId) = await _host.RegisterAsync("buyer@example.com");
        (_admin, _) = await _host.RegisterAsync("admin.payments@ekariyerim.com", admin: true);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<CheckoutResponse> PayAsync(string plan = "monthly", long? total = null, HttpClient? client = null)
    {
        var response = await (client ?? _user).PostAsJsonAsync("/api/payments/checkout",
            new StartCheckoutRequest(plan, "Ada Lovelace", "Somewhere 1", "+90 555 000 00 00", true), PaymentTestHost.JsonOptions);
        response.EnsureSuccessStatusCode();
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions))!;
        (await _host.NotifyAsync(checkout.MerchantOid, "success", total ?? checkout.AmountMinor)).EnsureSuccessStatusCode();
        return checkout;
    }

    private Task<PaymentOrder> OrderAsync(Guid id) => _host.WithDbAsync(db => db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == id));

    private Task<ProEntitlement?> EntitlementAsync() =>
        _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == _userId));

    [Fact]
    public async Task The_User_Requests_The_Admin_Refunds_In_Full_And_Pro_Is_Wound_Back()
    {
        var checkout = await PayAsync();
        (await EntitlementAsync())!.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));

        var request = await _user.PostAsJsonAsync($"/api/payments/orders/{checkout.OrderId}/refund-request",
            new RequestRefundRequest("Beklediğim gibi değildi."), PaymentTestHost.JsonOptions);
        request.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await request.Content.ReadFromJsonAsync<PaymentOrderResponse>(PaymentTestHost.JsonOptions))!.Status.ShouldBe("RefundRequested");

        var queue = await _admin.GetFromJsonAsync<List<AdminPaymentOrderResponse>>("/api/admin/payments/refund-requests", PaymentTestHost.JsonOptions);
        queue!.Single().Id.ShouldBe(checkout.OrderId);
        queue!.Single().RefundReason.ShouldBe("Beklediğim gibi değildi.");

        var refund = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/refund", new AdminRefundRequest(null), PaymentTestHost.JsonOptions);
        refund.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refunded = await refund.Content.ReadFromJsonAsync<AdminPaymentOrderResponse>(PaymentTestHost.JsonOptions);
        refunded!.Status.ShouldBe("Refunded");
        refunded.RefundedAmountMinor.ShouldBe(29900);
        refunded.RefundReferenceNo.ShouldBe(checkout.OrderId.ToString("N"));

        // What PayTR was asked, and how it was signed.
        var (path, form) = _host.PayTr.Requests.Single(r => r.Path.EndsWith("/odeme/iade"));
        form["merchant_oid"].ShouldBe(checkout.MerchantOid);
        form["return_amount"].ShouldBe("299.00");
        form["reference_no"].ShouldBe(checkout.OrderId.ToString("N"));
        form["paytr_token"].ShouldBe(PayTrSignature.RefundHash(PaymentTestHost.MerchantId, checkout.MerchantOid, "299.00",
            PaymentTestHost.MerchantSalt, PaymentTestHost.MerchantKey));

        // The period this order added is gone; the entitlement is no longer active.
        var entitlement = await EntitlementAsync();
        entitlement!.ActiveUntil.ShouldBe(PaymentTestHost.Start);
        entitlement.IsActive(PaymentTestHost.Start).ShouldBeFalse();

        var evidence = await _host.WithDbAsync(db => db.PaymentNotifications.AsNoTracking()
            .Where(n => n.MerchantOid == checkout.MerchantOid && n.Outcome == PaymentNotificationOutcome.RefundRecorded).SingleAsync());
        evidence.TotalAmountMinor.ShouldBe(29900);
        (await _user.GetFromJsonAsync<PaymentOrderResponse>($"/api/payments/orders/{checkout.OrderId}", PaymentTestHost.JsonOptions))!.CanRequestRefund.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Full_Refund_Keeps_Days_From_Other_Orders()
    {
        var first = await PayAsync("monthly");
        var second = await PayAsync("yearly");
        (await EntitlementAsync())!.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1).AddYears(1));

        (await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{second.OrderId}/refund", new AdminRefundRequest(null), PaymentTestHost.JsonOptions))
            .EnsureSuccessStatusCode();

        (await EntitlementAsync())!.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        (await OrderAsync(first.OrderId)).Status.ShouldBe(PaymentOrderStatus.Paid);
    }

    [Fact]
    public async Task A_Partial_Refund_Leaves_Pro_Running()
    {
        var checkout = await PayAsync();

        var response = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/refund", new AdminRefundRequest(10000), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.PartiallyRefunded);
        order.RefundableAmountMinor.ShouldBe(19900);
        (await EntitlementAsync())!.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        _host.PayTr.Requests.Single(r => r.Path.EndsWith("/odeme/iade")).Form["return_amount"].ShouldBe("100.00");

        // More than what is left is refused before PayTR is asked.
        var tooMuch = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/refund", new AdminRefundRequest(20000), PaymentTestHost.JsonOptions);
        tooMuch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _host.PayTr.Requests.Count(r => r.Path.EndsWith("/odeme/iade")).ShouldBe(1);
    }

    [Fact]
    public async Task PayTRs_Refusal_Leaves_The_Order_Untouched_And_Reaches_The_Admin()
    {
        var checkout = await PayAsync();
        _host.PayTr.RefundResponse = _ => StubPayTrHandler.Json("""{"status":"error","err_no":"006","err_msg":"Toplam iade tutari odeme tutarindan fazla olamaz"}""");

        var response = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/refund", new AdminRefundRequest(null), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("006 Toplam iade tutari");
        (await OrderAsync(checkout.OrderId)).Status.ShouldBe(PaymentOrderStatus.Paid);
        (await EntitlementAsync())!.ActiveUntil.ShouldBe(PaymentTestHost.Start.AddMonths(1));
        (await _host.WithDbAsync(db => db.PaymentNotifications.CountAsync(n => n.MerchantOid == checkout.MerchantOid && n.Outcome == PaymentNotificationOutcome.Error))).ShouldBe(1);
    }

    [Fact]
    public async Task Marking_Refunded_By_Hand_Records_Without_Calling_PayTR()
    {
        var checkout = await PayAsync();
        var callsBefore = _host.PayTr.Requests.Count;

        var response = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/mark-refunded",
            new MarkRefundedRequest(29900, "PANEL-42"), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _host.PayTr.Requests.Count.ShouldBe(callsBefore);
        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Refunded);
        order.RefundReferenceNo.ShouldBe("PANEL-42");
        (await EntitlementAsync())!.IsActive(PaymentTestHost.Start).ShouldBeFalse();
    }

    [Fact]
    public async Task Rejecting_A_Request_Returns_The_Order_To_Paid()
    {
        var checkout = await PayAsync();
        (await _user.PostAsJsonAsync($"/api/payments/orders/{checkout.OrderId}/refund-request", new RequestRefundRequest("x"), PaymentTestHost.JsonOptions)).EnsureSuccessStatusCode();

        var response = await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.OrderId}/reject-refund", new RejectRefundRequest("14 gün geçti."), PaymentTestHost.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await OrderAsync(checkout.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.RefundRejectionNote.ShouldBe("14 gün geçti.");
        (await EntitlementAsync())!.IsActive(PaymentTestHost.Start).ShouldBeTrue();
    }

    [Fact]
    public async Task Refund_Requests_Are_Refused_For_Unpaid_Orders_Duplicates_And_Other_Peoples_Orders()
    {
        var pending = await (await _user.PostAsJsonAsync("/api/payments/checkout",
            new StartCheckoutRequest("yearly", "A", "B", "555", true), PaymentTestHost.JsonOptions)).Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        (await _user.PostAsJsonAsync($"/api/payments/orders/{pending!.OrderId}/refund-request", new RequestRefundRequest("x"), PaymentTestHost.JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var paid = await PayAsync();
        (await _user.PostAsJsonAsync($"/api/payments/orders/{paid.OrderId}/refund-request", new RequestRefundRequest("x"), PaymentTestHost.JsonOptions)).EnsureSuccessStatusCode();
        (await _user.PostAsJsonAsync($"/api/payments/orders/{paid.OrderId}/refund-request", new RequestRefundRequest("y"), PaymentTestHost.JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var (other, _) = await _host.RegisterAsync("other.refund@example.com");
        (await other.PostAsJsonAsync($"/api/payments/orders/{paid.OrderId}/refund-request", new RequestRefundRequest("x"), PaymentTestHost.JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_Routes_Are_Forbidden_To_Everyone_Else()
    {
        (await _user.GetAsync("/api/admin/payments/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _user.GetAsync("/api/admin/payments/orders")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _user.PostAsJsonAsync($"/api/admin/payments/orders/{Guid.NewGuid()}/refund", new AdminRefundRequest(null), PaymentTestHost.JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _host.AnonymousClient().GetAsync("/api/admin/payments/summary")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_Summary_Groups_By_Istanbul_Month_Counts_Refunds_Where_They_Happened_And_Skips_Test_Orders()
    {
        // A real (non-test) sale on 31 Aug 22:30 UTC = 1 Sep 01:30 Istanbul, refunded in September; a
        // test-mode sale; a failed and a cancelled order. The clock decides "now" for the summary.
        _host.Clock.Set(new DateTimeOffset(2026, 8, 31, 22, 30, 0, TimeSpan.Zero));
        var augustSale = await PayAsync("monthly");
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var order = await db.PaymentOrders.SingleAsync(o => o.Id == augustSale.OrderId);
            // Flip the row to a live sale: the summary must not count test-mode charges.
            await db.PaymentOrders.Where(o => o.Id == augustSale.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.TestMode, false));
        });
        var testSale = await PayAsync("yearly");

        _host.Clock.Set(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
        (await _admin.PostAsJsonAsync($"/api/admin/payments/orders/{augustSale.OrderId}/refund", new AdminRefundRequest(10000), PaymentTestHost.JsonOptions)).EnsureSuccessStatusCode();
        await _host.WithDbAsync(db => db.PaymentNotifications.Where(n => n.Outcome == PaymentNotificationOutcome.RefundRecorded)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.TestMode, false)));

        var failed = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("monthly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        (await _host.NotifyAsync(failed!.MerchantOid, "failed", 29900, new Dictionary<string, string> { ["failed_reason_code"] = "2" })).EnsureSuccessStatusCode();
        var cancelled = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("monthly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        (await _user.PostAsync($"/api/payments/orders/{cancelled!.OrderId}/cancel", null)).EnsureSuccessStatusCode();

        _host.Clock.Set(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        var summary = await _admin.GetFromJsonAsync<PaymentsSummaryResponse>("/api/admin/payments/summary?months=3", PaymentTestHost.JsonOptions);

        summary!.Months.Select(m => m.Month).ShouldBe(["2026-09", "2026-08", "2026-07"]);
        var september = summary.CurrentMonth;
        september.Month.ShouldBe("2026-09");
        september.PaidCount.ShouldBe(1);           // the live sale; the test-mode yearly is excluded
        september.GrossMinor.ShouldBe(29900);
        september.RefundedMinor.ShouldBe(10000);
        september.RefundCount.ShouldBe(1);
        september.NetMinor.ShouldBe(19900);
        september.FailedCount.ShouldBe(1);
        september.CancelledCount.ShouldBe(1);
        summary.Months.Single(m => m.Month == "2026-08").PaidCount.ShouldBe(0);
        summary.ActiveProUsers.ShouldBe(1);
        summary.TestOrdersLast30Days.ShouldBe(1);
        summary.OpenRefundRequests.ShouldBe(0);
        summary.Currency.ShouldBe("TL");
    }

    [Fact]
    public async Task Orders_Can_Be_Listed_Filtered_Searched_And_Opened_With_Their_Timeline()
    {
        var paid = await PayAsync();
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        var pending = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("yearly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        var all = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>("/api/admin/payments/orders", PaymentTestHost.JsonOptions);
        all!.TotalCount.ShouldBe(2);
        all.Items.First().Id.ShouldBe(pending!.OrderId); // newest first
        all.Items.ShouldAllBe(o => o.Email == "buyer@example.com");

        var onlyPaid = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>("/api/admin/payments/orders?status=paid", PaymentTestHost.JsonOptions);
        onlyPaid!.Items.Single().Id.ShouldBe(paid.OrderId);

        var byOid = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>($"/api/admin/payments/orders?q={paid.MerchantOid}", PaymentTestHost.JsonOptions);
        byOid!.Items.Single().Id.ShouldBe(paid.OrderId);

        var byEmail = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>("/api/admin/payments/orders?q=buyer@", PaymentTestHost.JsonOptions);
        byEmail!.TotalCount.ShouldBe(2);

        var thisMonth = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>("/api/admin/payments/orders?month=2026-09", PaymentTestHost.JsonOptions);
        thisMonth!.TotalCount.ShouldBe(2);
        var lastMonth = await _admin.GetFromJsonAsync<PagedResult<AdminPaymentOrderResponse>>("/api/admin/payments/orders?month=2026-08", PaymentTestHost.JsonOptions);
        lastMonth!.TotalCount.ShouldBe(0);

        var detail = await _admin.GetFromJsonAsync<AdminPaymentOrderDetailResponse>($"/api/admin/payments/orders/{paid.OrderId}", PaymentTestHost.JsonOptions);
        detail!.Order.BillingName.ShouldBe("Ada Lovelace");
        detail.Order.MerchantOid.ShouldBe(paid.MerchantOid);
        detail.Notifications.Single().Outcome.ShouldBe("Applied");
        detail.Entitlement!.IsActive.ShouldBeTrue();

        (await _admin.GetAsync($"/api/admin/payments/orders/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Alerts_Surface_Bad_Hashes_Unknown_Orders_Late_Successes_And_Amount_Mismatches()
    {
        await _host.NotifyAsync("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "success", 100);
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        var mismatched = await PayAsync(total: 31000);
        _host.Clock.Advance(TimeSpan.FromMinutes(1));
        var pending = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("yearly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);
        await _host.NotifyAsync(pending!.MerchantOid, "success", 1, hashOverride: "bad");

        var alerts = await _admin.GetFromJsonAsync<PaymentAlertsResponse>("/api/admin/payments/alerts", PaymentTestHost.JsonOptions);

        alerts!.Notifications.Select(n => n.Outcome).ShouldBe(["BadHash", "UnknownOrder"]);
        alerts.AmountMismatches.Single().Id.ShouldBe(mismatched.OrderId);
    }

    [Fact]
    public async Task An_Admin_Can_Close_A_Pending_Order()
    {
        var pending = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("yearly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        var response = await _admin.PostAsync($"/api/admin/payments/orders/{pending!.OrderId}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await OrderAsync(pending.OrderId);
        order.Status.ShouldBe(PaymentOrderStatus.Cancelled);
        order.CancelledByUserId.ShouldNotBeNull();
        order.CancelledByUserId.ShouldNotBe(_userId);
    }

    [Fact]
    public async Task The_Data_Export_Includes_Payments_And_The_Entitlement_Without_Provider_Internals()
    {
        var checkout = await PayAsync();

        var export = await _user.GetStringAsync("/api/users/me/export");

        export.ShouldContain("\"payments\"");
        export.ShouldContain(checkout.OrderId.ToString());
        export.ShouldContain("Ada Lovelace");
        export.ShouldContain("\"proEntitlement\"");
        export.ShouldContain("PayTr");
        export.ShouldNotContain(checkout.MerchantOid);
        export.ShouldNotContain("stub-iframe-token");
        export.ShouldNotContain(PaymentTestHost.MerchantId);
    }

    [Fact]
    public async Task Deleting_The_Account_Keeps_The_Order_Without_Its_User()
    {
        var checkout = await PayAsync();

        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == _userId));
            await db.SaveChangesAsync();
        });

        var order = await OrderAsync(checkout.OrderId);
        order.UserId.ShouldBeNull();
        order.Status.ShouldBe(PaymentOrderStatus.Paid);
        order.BillingName.ShouldBe("Ada Lovelace");
        (await EntitlementAsync()).ShouldBeNull();
        (await _admin.GetFromJsonAsync<AdminPaymentOrderDetailResponse>($"/api/admin/payments/orders/{checkout.OrderId}", PaymentTestHost.JsonOptions))!
            .Entitlement.ShouldBeNull();
    }
}

/// <summary>The two Hangfire jobs, called directly with a movable clock.</summary>
[Collection(IntegrationTestCollection.Name)]
public class PaymentMaintenanceTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private PaymentTestHost _host = null!;
    private HttpClient _user = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _host = await PaymentTestHost.CreateAsync(shared, nameof(PaymentMaintenanceTests));
        (_user, _userId) = await _host.RegisterAsync("maintenance@example.com", locale: "en");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<int> RunAsync(Func<IPaymentMaintenanceService, Task<int>> job)
    {
        var result = 0;
        return _host.WithScopeAsync(async sp => result = await job(sp.GetRequiredService<IPaymentMaintenanceService>())).ContinueWith(_ => result);
    }

    [Fact]
    public async Task Pending_Orders_Expire_After_The_Window_Plus_Grace_And_Not_Before()
    {
        var checkout = await (await _user.PostAsJsonAsync("/api/payments/checkout", new StartCheckoutRequest("monthly", "A", "B", "555", true), PaymentTestHost.JsonOptions))
            .Content.ReadFromJsonAsync<CheckoutResponse>(PaymentTestHost.JsonOptions);

        _host.Clock.Advance(TimeSpan.FromMinutes(40)); // window 30 + grace 15 not yet reached
        (await RunAsync(s => s.ExpirePendingOrdersAsync(CancellationToken.None))).ShouldBe(0);

        _host.Clock.Advance(TimeSpan.FromMinutes(10));
        (await RunAsync(s => s.ExpirePendingOrdersAsync(CancellationToken.None))).ShouldBe(1);
        (await RunAsync(s => s.ExpirePendingOrdersAsync(CancellationToken.None))).ShouldBe(0);

        var order = await _user.GetFromJsonAsync<PaymentOrderResponse>($"/api/payments/orders/{checkout!.OrderId}", PaymentTestHost.JsonOptions);
        order!.Status.ShouldBe("Expired");
    }

    [Fact]
    public async Task Expiry_Reminders_Go_Out_Once_Per_Period_In_The_Users_Language()
    {
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.ProEntitlements.Add(ProEntitlement.Grant(_userId, PaymentTestHost.Start.AddDays(2), ProEntitlementSource.PayTr, PaymentTestHost.Start));
            (await db.Users.SingleAsync(u => u.Id == _userId)).PreferredLanguage = "en";
            await db.SaveChangesAsync();
        });

        (await RunAsync(s => s.SendExpiryRemindersAsync(CancellationToken.None))).ShouldBe(1);
        (await RunAsync(s => s.SendExpiryRemindersAsync(CancellationToken.None))).ShouldBe(0);

        var entitlement = await _host.WithDbAsync(db => db.ProEntitlements.AsNoTracking().SingleAsync(e => e.UserId == _userId));
        entitlement.ExpiryReminderSentFor.ShouldBe(PaymentTestHost.Start.AddDays(2));

        // A new period (extended by a payment) earns a new reminder when its own end comes near.
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            (await db.ProEntitlements.SingleAsync(e => e.UserId == _userId)).Extend(PaymentTestHost.Start.AddMonths(1), ProEntitlementSource.PayTr, PaymentTestHost.Start);
            await db.SaveChangesAsync();
        });
        (await RunAsync(s => s.SendExpiryRemindersAsync(CancellationToken.None))).ShouldBe(0);
        _host.Clock.Set(PaymentTestHost.Start.AddMonths(1).AddDays(-2));
        (await RunAsync(s => s.SendExpiryRemindersAsync(CancellationToken.None))).ShouldBe(1);
    }

    [Fact]
    public async Task No_Reminder_For_Lapsed_Revoked_Or_Far_Away_Entitlements()
    {
        var (_, lapsedId) = await _host.RegisterAsync("lapsed@example.com");
        var (_, revokedId) = await _host.RegisterAsync("revoked@example.com");
        var (_, farId) = await _host.RegisterAsync("far@example.com");
        await _host.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.ProEntitlements.Add(ProEntitlement.Grant(lapsedId, PaymentTestHost.Start.AddDays(-1), ProEntitlementSource.PayTr, PaymentTestHost.Start));
            var revoked = ProEntitlement.Grant(revokedId, PaymentTestHost.Start.AddDays(1), ProEntitlementSource.Manual, PaymentTestHost.Start);
            revoked.Revoke(PaymentTestHost.Start);
            db.ProEntitlements.Add(revoked);
            db.ProEntitlements.Add(ProEntitlement.Grant(farId, PaymentTestHost.Start.AddDays(30), ProEntitlementSource.PayTr, PaymentTestHost.Start));
            await db.SaveChangesAsync();
        });

        (await RunAsync(s => s.SendExpiryRemindersAsync(CancellationToken.None))).ShouldBe(0);
    }
}
