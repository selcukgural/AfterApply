using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Identity;

public class PasswordResetTests : IAsyncLifetime
{
    private const string RegisteredPassword = "P@ssw0rd123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private CapturingEmailSender _emailSender = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");

            // Never call the real Resend API from tests — capture what would have been sent
            // instead, so a test can both assert "an email was sent" and pull the real token out
            // of the link to drive the rest of the flow.
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<CapturingEmailSender>();
                services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _emailSender = (CapturingEmailSender)_factory.Services.GetRequiredService<IEmailSender>();
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

    private async Task<AuthResponse> RegisterAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, RegisteredPassword, "Reset", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    private static (string Email, string Token) ParseResetLink(string resetLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
        return (query["email"].ToString(), query["token"].ToString());
    }

    // Sending is enqueued via Hangfire, not awaited inline within the request (see
    // AuthService.ForgotPasswordAsync/ResetPasswordAsync) — same reason CsvImportTests/
    // LinkedInImportTests poll instead of expecting a synchronous result.
    private async Task<string> WaitForResetLinkAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_emailSender.LastResetLink is not null)
            {
                return _emailSender.LastResetLink;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Password reset email job did not run within 30s.");
    }

    private async Task WaitForPasswordChangedEmailAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_emailSender.PasswordChangedCount > 0)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Password changed email job did not run within 30s.");
    }

    [Fact]
    public async Task ForgotPassword_For_Unknown_Email_Returns_NoContent_And_Sends_No_Email()
    {
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("no-such-user@example.com"), JsonOptions);

        // No polling needed here: AuthService.ForgotPasswordAsync returns without ever enqueuing a
        // job for an unknown email, so there's nothing async to race against.
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _emailSender.LastResetLink.ShouldBeNull();
    }

    [Fact]
    public async Task ForgotPassword_For_Registered_Email_Sends_Reset_Link()
    {
        const string email = "forgot.test@example.com";
        await RegisterAsync(email);
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var resetLink = await WaitForResetLinkAsync();
        resetLink.ShouldContain("/reset-password");
        _emailSender.LastLocale.ShouldBe("tr");

        var (linkEmail, token) = ParseResetLink(resetLink);
        linkEmail.ShouldBe(email);
        token.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPassword_With_Bogus_Token_Is_Rejected()
    {
        const string email = "badtoken.test@example.com";
        await RegisterAsync(email);
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, "not-a-real-token", "N3wStr0ng!Passw0rd"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_With_Weak_New_Password_Is_Rejected()
    {
        const string email = "weakpw.test@example.com";
        await RegisterAsync(email);
        var client = _factory!.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var (_, token) = ParseResetLink(await WaitForResetLinkAsync());

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "weak"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_With_Valid_Token_Revokes_Existing_Refresh_Tokens()
    {
        const string email = "reset.revoke@example.com";
        var oldAuth = await RegisterAsync(email);
        var client = _factory!.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var (_, token) = ParseResetLink(await WaitForResetLinkAsync());

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "N3wStr0ng!Passw0rd"), JsonOptions);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(oldAuth.RefreshToken), JsonOptions);
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_With_Valid_Token_Allows_Login_With_New_Password_Only()
    {
        const string email = "reset.login@example.com";
        const string newPassword = "N3wStr0ng!Passw0rd";
        await RegisterAsync(email);
        var client = _factory!.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var (_, token) = ParseResetLink(await WaitForResetLinkAsync());

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, newPassword), JsonOptions);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await WaitForPasswordChangedEmailAsync();

        var newLoginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, newPassword), JsonOptions);
        newLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

internal sealed class CapturingEmailSender : IEmailSender
{
    public string? LastResetLink { get; private set; }

    public string? LastLocale { get; private set; }

    public int PasswordChangedCount { get; private set; }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string locale, CancellationToken cancellationToken)
    {
        LastResetLink = resetLink;
        LastLocale = locale;
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedEmailAsync(string toEmail, string locale, CancellationToken cancellationToken)
    {
        PasswordChangedCount++;
        LastLocale = locale;
        return Task.CompletedTask;
    }
}
