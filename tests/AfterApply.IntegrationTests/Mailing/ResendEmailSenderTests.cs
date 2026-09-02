using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Mailing;

/// <summary>Exercises the real ResendEmailSender (unlike PasswordResetTests, which swaps IEmailSender
/// out entirely) — proves the EmailTemplates table rows are actually read, the right locale is
/// picked, and "{{ResetLink}}" gets substituted, by capturing the outbound HTTP call instead of the
/// send itself.</summary>
public class ResendEmailSenderTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private CapturingHttpMessageHandler _handler = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _handler = new CapturingHttpMessageHandler();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");
            // Non-empty so ResendEmailSender doesn't short-circuit on "not configured" — the real
            // template lookup + HTTP call still happen, just against the captured handler below
            // instead of the real Resend API.
            builder.UseSetting("Resend:ApiKey", "test-key");

            builder.ConfigureTestServices(services =>
            {
                // AddHttpClient<IEmailSender, ResendEmailSender>() (Program's own registration)
                // names its client after IEmailSender's short type name — reconfiguring that same
                // name here overrides just the handler, leaving the real ResendEmailSender (an
                // internal type, deliberately not exposed to this test project) as the
                // implementation under test.
                services.AddSingleton(_handler);
                services.AddHttpClient("IEmailSender")
                    .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<CapturingHttpMessageHandler>());
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Reset", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    // Sending is enqueued via Hangfire, not awaited inline within the request — see
    // AuthService.ForgotPasswordAsync/ResetPasswordAsync — so this polls the same way
    // PasswordResetTests does. `sentBefore` distinguishes "a new email arrived" from "the
    // previous one is still sitting there" when a test triggers two sends in a row.
    private async Task<ResendPayload> WaitForSentEmailAsync(int sentBefore = 0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_handler.RequestCount > sentBefore)
            {
                return JsonSerializer.Deserialize<ResendPayload>(_handler.LastRequestBody!, JsonOptions)!;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Resend send was not captured within 30s.");
    }

    [Fact]
    public async Task ForgotPassword_Uses_Turkish_Template_By_Default_With_Link_Substituted()
    {
        var client = _factory!.CreateClient();
        const string email = "resend.tr@example.com";
        await RegisterAsync(client, email);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);

        var payload = await WaitForSentEmailAsync();
        payload.Subject.ShouldBe("e-kariyerim şifre sıfırlama");
        payload.Html.ShouldContain("Şifremi sıfırla");
        payload.Html.ShouldNotContain("{{ResetLink}}");
        payload.Html.ShouldContain("/reset-password?email=");
        payload.To.ShouldContain(email);
    }

    [Fact]
    public async Task ForgotPassword_Uses_English_Template_When_Accept_Language_Is_En()
    {
        var client = _factory!.CreateClient();
        const string email = "resend.en@example.com";
        await RegisterAsync(client, email);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new ForgotPasswordRequest(email), options: JsonOptions)
        };
        request.Headers.AcceptLanguage.ParseAdd("en");
        await client.SendAsync(request);

        var payload = await WaitForSentEmailAsync();
        payload.Subject.ShouldBe("e-kariyerim password reset");
        payload.Html.ShouldContain("Reset my password");
        payload.Html.ShouldNotContain("{{ResetLink}}");
    }

    [Fact]
    public async Task ResetPassword_Sends_PasswordChanged_Template_Distinct_From_Reset_Template()
    {
        var client = _factory!.CreateClient();
        const string email = "resend.changed@example.com";
        await RegisterAsync(client, email);

        var beforeReset = _handler.RequestCount;
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var resetPayload = await WaitForSentEmailAsync(beforeReset);
        var (linkEmail, token) = ParseResetLink(ExtractHref(resetPayload.Html));
        linkEmail.ShouldBe(email);

        var beforeChanged = _handler.RequestCount;
        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "N3wStr0ng!Passw0rd"), JsonOptions);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var changedPayload = await WaitForSentEmailAsync(beforeChanged);
        changedPayload.Subject.ShouldBe("e-kariyerim şifreniz değiştirildi");
        changedPayload.Html.ShouldContain("güvenlik amacıyla sonlandırıldı");
        changedPayload.Html.ShouldNotContain("Şifremi sıfırla");
    }

    private static string ExtractHref(string html) => Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value;

    private static (string Email, string Token) ParseResetLink(string resetLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
        return (query["email"].ToString(), query["token"].ToString());
    }

    private sealed record ResendPayload(string From, string[] To, string Subject, string Html);
}

internal sealed class CapturingHttpMessageHandler : DelegatingHandler
{
    public string? LastRequestBody { get; private set; }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        RequestCount++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test-email-id\"}") };
    }
}
