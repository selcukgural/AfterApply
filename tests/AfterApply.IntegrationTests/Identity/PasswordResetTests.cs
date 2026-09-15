using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

namespace AfterApply.IntegrationTests.Identity;

public sealed class PasswordResetProfile : IHostProfile
{
    public CapturingEmailSender Emails { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");

        // Never call the real Resend API from tests — capture what would have been sent
        // instead, so a test can both assert "an email was sent" and pull the real token out
        // of the link to drive the rest of the flow.
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Emails));
    }

    public void Reset() => Emails.Reset();
}

[Collection(IntegrationTestCollection.Name)]
public class PasswordResetTests(ApiHost<PasswordResetProfile> host) : IClassFixture<ApiHost<PasswordResetProfile>>, IAsyncLifetime
{
    private const string RegisteredPassword = "P@ssw0rd123!";

    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private CapturingEmailSender _emailSender => host.Profile.Emails;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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

    // Sending is enqueued as a background job, not awaited inline within the request (see
    // AuthService.ForgotPasswordAsync/ResetPasswordAsync); the job runs here.
    private async Task<string> WaitForResetLinkAsync()
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();
        return _emailSender.LastResetLink.ShouldNotBeNull("the password reset e-mail job did not send a link");
    }

    private async Task WaitForPasswordChangedEmailAsync()
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();
        _emailSender.PasswordChangedCount.ShouldBeGreaterThan(0, "the password changed e-mail job did not run");
    }

    [Fact]
    public async Task ForgotPassword_For_Unknown_Email_Returns_NoContent_And_Sends_No_Email()
    {
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("no-such-user@example.com"), JsonOptions);

        // AuthService.ForgotPasswordAsync returns without ever enqueuing a job for an unknown
        // email — and if it did, running it here would show.
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        host.Jobs.Pending.ShouldBeEmpty();
        await host.RunJobsAsync();
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
