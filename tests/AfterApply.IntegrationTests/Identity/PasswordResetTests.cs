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
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, RegisteredPassword, "Reset", "Test", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        return auth;
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

        // Job arguments are stored as plain text: the queued job names the account, and the address
        // and the reset token are only produced when it runs.
        var job = host.Jobs.Pending.ShouldHaveSingleItem();
        job.TargetType.ShouldBe(typeof(IAccountEmailJobs));
        job.Job.Args.ShouldNotContain(arg => arg is string && ((string)arg).Contains('@'));

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

    // A token minted from a stolen session must not outlive the reset the victim does to get rid
    // of it (2026-09-24).
    [Fact]
    public async Task ResetPassword_Also_Revokes_Personal_Access_Tokens()
    {
        const string email = "reset.pat@example.com";
        var auth = await RegisterAsync(email);
        var owner = _factory!.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var created = await owner.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension"), JsonOptions);
        var pat = (await created.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions))!;
        using var patClient = _factory.CreateClient();
        patClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pat.Token);
        (await patClient.GetAsync("/api/companies/search?q=acme")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var (_, token) = ParseResetLink(await WaitForResetLinkAsync());
        (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "N3wStr0ng!Passw0rd"), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await patClient.GetAsync("/api/companies/search?q=acme")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // One inbox cannot be flooded, and the day's send quota every other reset depends on cannot be
    // spent from one script. A throttled request answers exactly like any other.
    [Fact]
    public async Task A_Second_Reset_Email_Inside_The_Cooldown_Is_Not_Sent()
    {
        const string email = "reset.cooldown@example.com";
        await RegisterAsync(email);
        var client = _factory!.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForResetLinkAsync();

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        host.Jobs.Pending.ShouldBeEmpty();
    }

    // The reset link proves the address, so an account that never verified it becomes verified —
    // and whatever provider login somebody else attached to it goes.
    [Fact]
    public async Task Resetting_An_Unverified_Account_Verifies_It_And_Removes_Its_Provider_Logins()
    {
        const string email = "reset.unverified@example.com";
        var auth = await RegisterAsync(email);
        await host.WithDbAsync(async db =>
        {
            await db.Users.Where(u => u.Id == auth.User.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.EmailConfirmed, false));
            db.UserLogins.Add(new Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>
            {
                UserId = auth.User.Id, LoginProvider = "LinkedIn", ProviderKey = "li-someone-else", ProviderDisplayName = "LinkedIn"
            });
            await db.SaveChangesAsync();
        });

        var client = _factory!.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), JsonOptions);
        var (_, token) = ParseResetLink(await WaitForResetLinkAsync());
        (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "N3wStr0ng!Passw0rd"), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).EmailConfirmed.ShouldBeTrue();
        (await db.UserLogins.AnyAsync(l => l.UserId == auth.User.Id)).ShouldBeFalse();
    }
}
