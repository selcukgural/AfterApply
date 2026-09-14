using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Mailing;

public sealed class ResendEmailSenderProfile : IHostProfile
{
    public CapturingHttpMessageHandler Resend { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
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
            services.AddHttpClient("IEmailSender").ConfigurePrimaryHttpMessageHandler(() => Resend);
        });
    }

    public void Reset() => Resend.Reset();
}

/// <summary>Exercises the real ResendEmailSender (unlike PasswordResetTests, which swaps IEmailSender
/// out entirely) — proves the EmailTemplates table rows are actually read, the right locale is
/// picked, and "{{ResetLink}}" gets substituted, by capturing the outbound HTTP call instead of the
/// send itself.</summary>
[Collection(IntegrationTestCollection.Name)]
public class ResendEmailSenderTests(ApiHost<ResendEmailSenderProfile> host) : IClassFixture<ApiHost<ResendEmailSenderProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private CapturingHttpMessageHandler _handler => host.Profile.Resend;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Reset", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    // Sending is enqueued as a background job, not awaited inline within the request — see
    // AuthService.ForgotPasswordAsync/ResetPasswordAsync — so the job runs here first.
    // `sentBefore` distinguishes "a new email arrived" from "the previous one is still sitting
    // there" when a test triggers two sends in a row.
    private async Task<ResendPayload> WaitForSentEmailAsync(int sentBefore = 0)
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();
        _handler.RequestCount.ShouldBeGreaterThan(sentBefore, "no Resend send was captured");
        return JsonSerializer.Deserialize<ResendPayload>(_handler.LastRequestBody!, JsonOptions)!;
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

    [Fact]
    public async Task WeeklyJobsDigest_Fills_The_Template_And_Encodes_Scraped_Text()
    {
        using var scope = _factory!.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var before = _handler.RequestCount;

        // The title is the kind of thing a job site could carry; it must arrive as text.
        await sender.SendWeeklyJobsReadyEmailAsync("digest@example.com", "tr",
            new WeeklyJobsDigest(7, "<img src=x onerror=alert(1)> Developer", "Acme & Sons", 92, "https://ekariyerim.com/tr/weekly-jobs"),
            CancellationToken.None);

        var payload = await WaitForSentEmailAsync(before);
        payload.Subject.ShouldBe("Bu hafta size uyan 7 ilan hazır");
        payload.Html.ShouldContain("Bu hafta 7 ilan hazır");
        payload.Html.ShouldContain("&lt;img src=x onerror=alert(1)&gt; Developer");
        payload.Html.ShouldNotContain("<img src=x");
        payload.Html.ShouldContain("Acme &amp; Sons");
        payload.Html.ShouldContain("(%92)");
        payload.Html.ShouldContain("href=\"https://ekariyerim.com/tr/weekly-jobs\"");
        payload.Html.ShouldNotContain("{{");

        await sender.SendWeeklyJobsReadyEmailAsync("digest@example.com", "en",
            new WeeklyJobsDigest(1, "Backend Developer", "Acme", 80, "https://ekariyerim.com/en/weekly-jobs"), CancellationToken.None);
        (await WaitForSentEmailAsync(before + 1)).Subject.ShouldBe("1 postings that fit you are ready this week");
    }

    private static string ExtractHref(string html) => Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value;

    private static (string Email, string Token) ParseResetLink(string resetLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
        return (query["email"].ToString(), query["token"].ToString());
    }

    private sealed record ResendPayload(string From, string[] To, string Subject, string Html);
}

public sealed class CapturingHttpMessageHandler : DelegatingHandler
{
    public string? LastRequestBody { get; private set; }

    public int RequestCount { get; private set; }

    public void Reset()
    {
        LastRequestBody = null;
        RequestCount = 0;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        RequestCount++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"test-email-id\"}") };
    }
}
