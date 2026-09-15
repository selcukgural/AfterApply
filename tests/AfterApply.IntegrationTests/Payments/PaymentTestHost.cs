using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Persistence;
using AfterApply.IntegrationTests.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests.Payments;

/// <summary>
/// One configured host for the payment tests: PayTR switched on with test secrets, the PayTR
/// transport stubbed, e-mail captured, the clock movable. Hangfire's server stays off (jobs are
/// enqueued, not run) so the tests call the maintenance service directly and assert on what was
/// queued through the captured sender only where a job would have run inline.
/// </summary>
internal sealed class PaymentTestHost : IAsyncDisposable
{
    public const string MerchantId = "100200";
    public const string MerchantKey = "test-merchant-key";
    public const string MerchantSalt = "test-merchant-salt";
    public const long MonthlyPrice = 29900;
    public const long YearlyPrice = 299000;
    public const string ClientIp = "203.0.113.7";

    public static readonly DateTimeOffset Start = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public WebApplicationFactory<Program> Factory { get; }

    public StubPayTrHandler PayTr { get; } = new();

    public MutableTimeProvider Clock { get; } = new(Start);

    public CapturingEmailSender Emails => Factory.Services.GetRequiredService<CapturingEmailSender>();

    private PaymentTestHost(string postgres, Action<IDictionary<string, string?>>? configure)
    {
        var settings = new Dictionary<string, string?>
        {
            ["PayTr:Enabled"] = "true",
            ["PayTr:MerchantId"] = MerchantId,
            ["PayTr:MerchantKey"] = MerchantKey,
            ["PayTr:MerchantSalt"] = MerchantSalt,
            ["PayTr:TestMode"] = "true",
            ["PayTr:Plans:Monthly:AmountMinor"] = MonthlyPrice.ToString(),
            ["PayTr:Plans:Yearly:AmountMinor"] = YearlyPrice.ToString(),
            ["App:WebBaseUrl"] = "https://www.ekariyerim.com",
        };
        configure?.Invoke(settings);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient(nameof(IPayTrClient)).ConfigurePrimaryHttpMessageHandler(() => PayTr);
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<CapturingEmailSender>();
                services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
            });
        });
    }

    public static async Task<PaymentTestHost> CreateAsync(SharedInfrastructure shared, string name, Action<IDictionary<string, string?>>? configure = null)
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(name);
        return new PaymentTestHost(postgres, configure);
    }

    public async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email, string locale = "tr", bool admin = false)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ClientIp);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(locale);
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Ada", "Lovelace", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
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

    public async ValueTask DisposeAsync() => await TestHostDisposal.DisposeQuietlyAsync(Factory);
}
