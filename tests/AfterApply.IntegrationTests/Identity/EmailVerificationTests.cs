using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

public sealed class EmailVerificationProfile : IHostProfile
{
    public CapturingEmailSender Emails { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Emails));
    }

    public void Reset() => Emails.Reset();
}

/// <summary>
/// Email verification at sign-up (2026-09-24), end to end: the sign-up gets a ticket and no tokens,
/// the code arrives by email, the two together sign the user in. And the reason it exists — a
/// sign-up made under somebody else's address can never be completed by whoever made it.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class EmailVerificationTests(ApiHost<EmailVerificationProfile> host)
    : IClassFixture<ApiHost<EmailVerificationProfile>>, IAsyncLifetime
{
    private const string Password = "P@ssw0rd123!";
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private CapturingEmailSender Emails => host.Profile.Emails;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Sign_Up_Gets_A_Ticket_And_No_Tokens_Until_The_Emailed_Code_Comes_Back()
    {
        var client = host.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", Registration("new.person@example.com"), JsonOptions);

        register.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await register.Content.ReadAsStringAsync();
        body.ShouldNotContain("accessToken");
        var pending = JsonSerializer.Deserialize<EmailVerificationPendingResponse>(body, JsonOptions)!;
        pending.Email.ShouldBe("new.person@example.com");

        var code = await SentCodeAsync("new.person@example.com");
        code.Length.ShouldBe(6);

        var verified = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(pending.VerificationTicket, code), JsonOptions);
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await verified.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        (await client.GetAsync("/api/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.WithDbAsync(db => db.Users.SingleAsync(u => u.Id == auth.User.Id))).EmailConfirmed.ShouldBeTrue();

        // A ticket verifies once.
        (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(pending.VerificationTicket, code), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Code_Is_Not_Written_Into_The_Background_Job()
    {
        await host.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("job.args@example.com"), JsonOptions);

        var job = host.Jobs.Pending.Single(TestAccounts.IsVerificationCodeJob);
        job.Job.Args.OfType<string>().ShouldNotContain(a => a.Length == 6 && a.All(char.IsAsciiDigit));
    }

    [Fact]
    public async Task Signing_In_Before_Verifying_Answers_With_A_New_Ticket_Only_For_The_Right_Password()
    {
        var client = host.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Registration("later@example.com"), JsonOptions);
        var code = await SentCodeAsync("later@example.com");

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("later@example.com", "not-the-password"), JsonOptions);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrong.Content.ReadAsStringAsync()).ShouldNotContain("verificationTicket");

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("later@example.com", Password), JsonOptions);
        login.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var pending = (await login.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>(JsonOptions))!;

        // Inside the cooldown no second email went out, and the code already sent still works
        // with the new ticket.
        await host.RunJobsAsync();
        Emails.VerificationCodes.Count.ShouldBe(1);
        (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(pending.VerificationTicket, code), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Five_Wrong_Guesses_Burn_The_Code()
    {
        var client = host.CreateClient();
        var pending = await RegisterAsync(client, "guesser@example.com");
        var code = await SentCodeAsync("guesser@example.com");
        var wrong = code == "000000" ? "111111" : "000000";

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await VerifyErrorAsync(client, pending.VerificationTicket, wrong)).ShouldBe("AUTH_VERIFICATION_CODE_INVALID");
        }

        (await VerifyErrorAsync(client, pending.VerificationTicket, wrong)).ShouldBe("AUTH_VERIFICATION_CODE_EXPIRED");
        // Even the right code no longer works; a new one has to be requested.
        (await VerifyErrorAsync(client, pending.VerificationTicket, code)).ShouldBe("AUTH_VERIFICATION_CODE_EXPIRED");
    }

    [Fact]
    public async Task A_Resend_Inside_The_Cooldown_Sends_Nothing_And_Says_When_It_Can()
    {
        var client = host.CreateClient();
        var pending = await RegisterAsync(client, "impatient@example.com");
        await SentCodeAsync("impatient@example.com");

        var resend = await client.PostAsJsonAsync("/api/auth/verify-email/resend", new ResendVerificationCodeRequest(pending.VerificationTicket), JsonOptions);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await resend.Content.ReadFromJsonAsync<ResendVerificationCodeResponse>(JsonOptions))!;
        body.ResendAvailableAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        await host.RunJobsAsync();
        Emails.VerificationCodes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_Resend_After_The_Cooldown_Sends_A_New_Code_That_Replaces_The_Old_One()
    {
        var client = host.CreateClient();
        var pending = await RegisterAsync(client, "second.code@example.com");
        var first = await SentCodeAsync("second.code@example.com");
        await AgeDispatchesAsync(TimeSpan.FromSeconds(61));

        (await client.PostAsJsonAsync("/api/auth/verify-email/resend", new ResendVerificationCodeRequest(pending.VerificationTicket), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        await host.RunJobsAsync();
        Emails.VerificationCodes.Count.ShouldBe(2);
        var second = Emails.LastCodeFor("second.code@example.com")!;

        if (first != second)
        {
            (await VerifyErrorAsync(client, pending.VerificationTicket, first)).ShouldBe("AUTH_VERIFICATION_CODE_INVALID");
        }

        (await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(pending.VerificationTicket, second), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task One_Account_Receives_At_Most_Five_Codes_A_Day()
    {
        var client = host.CreateClient();
        var pending = await RegisterAsync(client, "flooded@example.com");
        await SentCodeAsync("flooded@example.com");

        for (var i = 0; i < 6; i++)
        {
            await AgeDispatchesAsync(TimeSpan.FromSeconds(61));
            await client.PostAsJsonAsync("/api/auth/verify-email/resend", new ResendVerificationCodeRequest(pending.VerificationTicket), JsonOptions);
            await host.RunJobsAsync();
        }

        Emails.VerificationCodes.Count(c => c.ToEmail == "flooded@example.com").ShouldBe(5);
    }

    // The attack the whole flow exists for: somebody signs up with the owner's address. The code
    // goes to the owner, who can do nothing with the stranger's ticket; and when the owner signs up
    // themselves, the stranger's sign-up is replaced — password, ticket and all.
    [Fact]
    public async Task A_Sign_Up_Under_Someone_Elses_Address_Is_Replaced_When_The_Owner_Signs_Up()
    {
        var stranger = host.CreateClient();
        var strangersPending = await RegisterAsync(stranger, "owner@example.com", "Stranger!Pass123");
        await host.RunJobsAsync();

        var owner = host.CreateClient();
        var ownersPending = await RegisterAsync(owner, "owner@example.com");
        await AgeDispatchesAsync(TimeSpan.FromSeconds(61));
        await owner.PostAsJsonAsync("/api/auth/verify-email/resend", new ResendVerificationCodeRequest(ownersPending.VerificationTicket), JsonOptions);
        var code = await SentCodeAsync("owner@example.com");

        // The stranger's ticket died with their sign-up; the owner's code is no use to it.
        (await VerifyErrorAsync(stranger, strangersPending.VerificationTicket, code)).ShouldBe("AUTH_VERIFICATION_EXPIRED");

        (await owner.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(ownersPending.VerificationTicket, code), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await stranger.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@example.com", "Stranger!Pass123"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await owner.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@example.com", Password), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.WithDbAsync(db => db.Users.CountAsync(u => u.Email == "owner@example.com"))).ShouldBe(1);
    }

    [Fact]
    public async Task A_Verified_Address_Still_Cannot_Be_Registered_Twice()
    {
        await host.RegisterAsync("taken@example.com");

        var again = await host.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("taken@example.com"), JsonOptions);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // An account that signed in before verification existed keeps its data; it simply verifies at
    // its next sign-in, and its old session does not renew in the meantime.
    [Fact]
    public async Task A_Session_From_Before_Verification_Existed_Does_Not_Renew()
    {
        var (_, auth) = await host.RegisterAsync("legacy@example.com");
        await host.WithDbAsync(db => db.Users.Where(u => u.Id == auth.User.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.EmailConfirmed, false)));

        var client = host.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("legacy@example.com", ApiHost.DefaultPassword), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Registering the address again is refused: this account has signed in, so it holds it.
        (await client.PostAsJsonAsync("/api/auth/register", Registration("legacy@example.com"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sign_Ups_Never_Verified_Are_Deleted_After_Seven_Days_But_Accounts_That_Signed_In_Are_Kept()
    {
        await host.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("abandoned@example.com"), JsonOptions);
        await host.CreateClient().PostAsJsonAsync("/api/auth/register", Registration("recent@example.com"), JsonOptions);
        var (_, legacy) = await host.RegisterAsync("legacy.kept@example.com");

        await host.WithDbAsync(async db =>
        {
            await db.Users.Where(u => u.Email == "abandoned@example.com" || u.Email == "legacy.kept@example.com")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.CreatedAt, DateTimeOffset.UtcNow.AddDays(-8))
                    .SetProperty(u => u.EmailConfirmed, false));
        });

        int deleted = 0;
        await host.WithScopeAsync(async services =>
            deleted = await services.GetRequiredService<IUnverifiedAccountCleanupService>().PurgeAsync(CancellationToken.None));

        deleted.ShouldBe(1);
        var remaining = await host.WithDbAsync(db => db.Users.Select(u => u.Email).ToListAsync());
        remaining.ShouldNotContain("abandoned@example.com");
        remaining.ShouldContain("recent@example.com");
        remaining.ShouldContain("legacy.kept@example.com");
        legacy.User.Email.ShouldBe("legacy.kept@example.com");
    }

    private static RegisterRequest Registration(string email, string password = Password) =>
        new(email, password, "", "", true);

    private async Task<EmailVerificationPendingResponse> RegisterAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(email, password), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await response.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>(JsonOptions))!;
    }

    private async Task<string> SentCodeAsync(string email)
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();
        return Emails.LastCodeFor(email).ShouldNotBeNull("no verification code was sent");
    }

    private static async Task<string> VerifyErrorAsync(HttpClient client, string ticket, string code)
    {
        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(ticket, code), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions);
        return problem!.RootElement.GetProperty("code").GetString()!;
    }

    /// <summary>Moves every recorded account email back in time, so the cooldown has passed
    /// without the test waiting it out.</summary>
    private Task AgeDispatchesAsync(TimeSpan by) => host.WithDbAsync(db => db.AuthEmailDispatches
        .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.SentAt, d => d.SentAt - by)));
}
