using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Persistence;
using AfterApply.IntegrationTests.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>
/// One configured host for the payment tests: PayTR switched on with test secrets, the PayTR
/// transport stubbed, e-mail captured, the clock movable. Jobs (the receipt, refund and reminder
/// e-mails) are recorded rather than run, so the tests call <c>RunJobsAsync</c> where a mail is
/// expected and assert on the captured sender.
/// </summary>
public class PaymentProfile : IHostProfile
{
    /// <summary>The feature switch; <see cref="PayTrFlagOffProfile" /> turns it off.</summary>
    protected virtual bool Enabled => true;

    public StubPayTrHandler PayTr { get; } = new();

    public MutableTimeProvider Clock { get; } = new(PaymentTestHost.Start);

    public CapturingEmailSender Emails { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayTr:Enabled"] = Enabled ? "true" : "false",
            ["PayTr:MerchantId"] = PaymentTestHost.MerchantId,
            ["PayTr:MerchantKey"] = PaymentTestHost.MerchantKey,
            ["PayTr:MerchantSalt"] = PaymentTestHost.MerchantSalt,
            ["PayTr:TestMode"] = "true",
            ["PayTr:Plans:Monthly:AmountMinor"] = PaymentTestHost.MonthlyPrice.ToString(),
            ["PayTr:Plans:Yearly:AmountMinor"] = PaymentTestHost.YearlyPrice.ToString(),
            ["App:WebBaseUrl"] = "https://www.ekariyerim.com",
        }));
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient(nameof(IPayTrClient)).ConfigurePrimaryHttpMessageHandler(() => PayTr);
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public void Reset()
    {
        PayTr.Reset();
        Clock.Reset();
        Emails.Reset();
    }
}

/// <summary>The same host with the switch off (the secrets stay): nothing of the checkout is
/// observable, except the notification endpoint, which only needs the secrets.</summary>
public sealed class PayTrFlagOffProfile : PaymentProfile
{
    protected override bool Enabled => false;
}

/// <summary>The payment tests' view of their class host: the constants the assertions quote, the
/// stub, the clock, the captured mail, and the helpers that drive PayTR's side of the protocol.</summary>
internal sealed class PaymentTestHost(ApiHost host)
{
    public const string MerchantId = "100200";
    public const string MerchantKey = "test-merchant-key";
    public const string MerchantSalt = "test-merchant-salt";
    public const long MonthlyPrice = 29900;
    public const long YearlyPrice = 299000;
    public const string ClientIp = "203.0.113.7";

    public static readonly DateTimeOffset Start = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    public WebApplicationFactory<Program> Factory => host;

    private PaymentProfile Profile => (PaymentProfile)host.Profile;

    public StubPayTrHandler PayTr => Profile.PayTr;

    public MutableTimeProvider Clock => Profile.Clock;

    public CapturingEmailSender Emails => Profile.Emails;

    /// <summary>Performs the e-mail jobs the last request enqueued.</summary>
    public Task RunJobsAsync() => host.RunJobsAsync();

    public async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email, string locale = "tr", bool admin = false)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ClientIp);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(locale);
        var auth = await TestAccounts.RegisterVerifiedAsync(client, Factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Ada", "Lovelace", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        if (admin)
        {
            user.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        return (client, user.Id);
    }

    public HttpClient AnonymousClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.9");
        return client;
    }

    /// <summary>Posts a notification the way PayTR does — form-encoded, signed with the test secrets.</summary>
    public Task<HttpResponseMessage> NotifyAsync(string merchantOid, string status, long totalAmountMinor,
        IDictionary<string, string>? extra = null, string? hashOverride = null)
    {
        var total = totalAmountMinor.ToString();
        var form = new Dictionary<string, string>
        {
            ["merchant_oid"] = merchantOid,
            ["status"] = status,
            ["total_amount"] = total,
            ["hash"] = hashOverride ?? PayTrSignature.CallbackHash(merchantOid, MerchantSalt, status, total, MerchantKey),
            ["payment_type"] = "card",
            ["currency"] = "TL",
            ["payment_amount"] = total,
            ["test_mode"] = "1",
        };
        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                form[key] = value;
            }
        }

        return AnonymousClient().PostAsync("/api/payments/paytr/callback", new FormUrlEncodedContent(form));
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }
}
