using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>
/// Sign in with LinkedIn, end to end against the real host — everything except the round-trip to
/// LinkedIn, which FakeLinkedInAuthClient replaces with "this code means this identity". Covers the
/// same shape of scenarios as GoogleSignInTests (already linked → signed in; verified email matches
/// an account → linked and signed in; unknown → sign-up step; the consent gate; rejections; a
/// password-less account) plus what is unique to LinkedIn: an identity with no verified email at all
/// forces a manual, required email on the sign-up form, that path can never hijack an existing
/// account by typing its address, and an unverified email is treated exactly like no email.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LinkedInSignInTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string ClientId = "test-linkedin-client";
    private const string RedirectUri = "http://localhost:3000/tr/auth/linkedin/callback";
    private const string Password = "P@ssw0rd123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FakeLinkedInAuthClient _linkedIn = new();
    private WebApplicationFactory<Program>? _factory;
    private WebApplicationFactory<Program>? _unconfiguredFactory;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(LinkedInSignInTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");
            builder.UseSetting("LinkedInAuth:ClientId", ClientId);
            builder.UseSetting("LinkedInAuth:ClientSecret", "test-secret");
            builder.ConfigureTestServices(services => services.AddSingleton<ILinkedInAuthClient>(_linkedIn));
        });

        // Same database, no LinkedIn client configured — the shape of a deployment that never set
        // the two secrets.
        _unconfiguredFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // The test host runs as Development and therefore loads the developer's user-secrets,
            // where a real LinkedInAuth client id/secret may well be set. Clear them so "not
            // configured" means exactly that, everywhere.
            builder.UseSetting("LinkedInAuth:ClientId", "");
            builder.UseSetting("LinkedInAuth:ClientSecret", "");
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_unconfiguredFactory is not null)
        {
            await _unconfiguredFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Config_Publishes_Availability_And_The_Public_Client_Id()
    {
        var configured = await _factory!.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        configured!.LinkedInAuth.ShouldBe(new LinkedInAuthConfigResponse(true, ClientId));

        var unconfigured = await _unconfiguredFactory!.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        unconfigured!.LinkedInAuth.ShouldBe(new LinkedInAuthConfigResponse(false, null));
    }

    [Fact]
    public async Task Both_Endpoints_Are_404_When_Not_Configured()
    {
        var client = _unconfiguredFactory!.CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/auth/linkedin",
            new LinkedInSignInRequest("code", RedirectUri), JsonOptions);
        signIn.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest("token", "Ada", "Lovelace", null, true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_New_LinkedIn_Account_With_A_Verified_Email_Gets_A_Signup_Step_And_Is_Created_Only_With_Consent()
    {
        var client = _factory!.CreateClient();
        var identity = new LinkedInIdentity("li-new-1", "new.linkedin@example.com", true, "Ada", "Lovelace");

        var signIn = await SignInAsync(client, identity);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await signIn.Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!;
        body.Auth.ShouldBeNull();
        body.PendingSignup.ShouldNotBeNull();
        body.PendingSignup.Email.ShouldBe("new.linkedin@example.com");
        // Nothing was created yet.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.AnyAsync(u => u.Email == "new.linkedin@example.com")).ShouldBeFalse();
        }

        var withoutConsent = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(body.PendingSignup.SignupToken, "Ada", "Lovelace", null, ConsentAccepted: false), JsonOptions);
        withoutConsent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(body.PendingSignup.SignupToken, "Augusta Ada", "King", null, ConsentAccepted: true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.Created);
        var auth = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
        auth.User.Email.ShouldBe("new.linkedin@example.com");
        auth.User.FirstName.ShouldBe("Augusta Ada");
        auth.User.HasPassword.ShouldBeFalse();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
            user.EmailConfirmed.ShouldBeTrue();
            user.PasswordHash.ShouldBeNull();
            (await db.UserLogins.SingleAsync(l => l.UserId == user.Id)).ProviderKey.ShouldBe("li-new-1");
        }

        // From now on the same LinkedIn account signs straight in — no sign-up step, same user.
        var again = await SignInAsync(_factory.CreateClient(), identity);
        var againBody = (await again.Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!;
        againBody.PendingSignup.ShouldBeNull();
        againBody.Auth!.User.Id.ShouldBe(auth.User.Id);
    }

    [Fact]
    public async Task A_Verified_Email_Matching_A_Password_Account_Is_Linked_And_Signed_In()
    {
        var client = _factory!.CreateClient();
        var registered = await RegisterAsync(client, "linked.li@example.com");
        registered.User.HasPassword.ShouldBeTrue();

        var signIn = await SignInAsync(client, new LinkedInIdentity("li-linked", "Linked.LI@Example.com", true, "L", "K"));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await signIn.Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!;
        body.PendingSignup.ShouldBeNull();
        body.Auth!.User.Id.ShouldBe(registered.User.Id);
        // Linking never strips the password: the account is now reachable both ways.
        body.Auth.User.HasPassword.ShouldBeTrue();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("linked.li@example.com", Password), JsonOptions);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == registered.User.Id);
        user.EmailConfirmed.ShouldBeTrue();
        (await db.UserLogins.CountAsync(l => l.UserId == user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task A_Code_LinkedIn_Does_Not_Recognise_Is_Rejected()
    {
        var client = _factory!.CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/auth/linkedin",
            new LinkedInSignInRequest("not-issued", RedirectUri), JsonOptions);

        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Redirect_Uri_Outside_Our_Web_Origin_Is_Rejected_Before_Reaching_LinkedIn()
    {
        var client = _factory!.CreateClient();
        var code = _linkedIn.IssueCode(new LinkedInIdentity("li-redirect", "redirect@example.com", true, null, null));

        var signIn = await client.PostAsJsonAsync("/api/auth/linkedin",
            new LinkedInSignInRequest(code, "https://evil.example.com/tr/auth/linkedin/callback"), JsonOptions);

        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _linkedIn.Exchanges.ShouldNotContain(e => e.Code == code);
    }

    [Fact]
    public async Task A_Tampered_Or_Foreign_Signup_Token_Is_A_Validation_Problem()
    {
        var client = _factory!.CreateClient();
        var registered = await RegisterAsync(client, "foreign.token.li@example.com");

        // One of our own access tokens: same signing key, wrong audience/purpose.
        var withAccessToken = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(registered.AccessToken, "A", "B", null, true), JsonOptions);
        withAccessToken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var withGarbage = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest("garbage", "A", "B", null, true), JsonOptions);
        withGarbage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_LinkedIn_Only_Account_Is_Deleted_Without_A_Password()
    {
        var client = _factory!.CreateClient();
        var pending = (await (await SignInAsync(client, new LinkedInIdentity("li-delete", "delete.li@example.com", true, "D", "G")))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;
        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "D", "G", null, true), JsonOptions);
        var auth = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await DeleteAccountAsync(client, password: null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // --- LinkedIn-specific: an identity with no usable (verified) email ---

    [Fact]
    public async Task An_Emailless_Identity_Requires_A_Manually_Entered_Email_On_Signup()
    {
        var client = _factory!.CreateClient();
        var identity = new LinkedInIdentity("li-noemail-1", null, false, "Grace", "Hopper");

        var pending = (await (await SignInAsync(client, identity))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;
        pending.Email.ShouldBeNull();

        var withoutEmail = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "Grace", "Hopper", null, true), JsonOptions);
        withoutEmail.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_Emailless_Identity_Registers_Successfully_With_A_Fresh_Manual_Email_And_Stays_Unconfirmed()
    {
        var client = _factory!.CreateClient();
        var identity = new LinkedInIdentity("li-noemail-2", null, false, "Grace", "Hopper");

        var pending = (await (await SignInAsync(client, identity))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;

        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "Grace", "Hopper", "grace.manual@example.com", true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.Created);
        var auth = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
        auth.User.Email.ShouldBe("grace.manual@example.com");
        auth.User.HasPassword.ShouldBeFalse();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
        // LinkedIn never vouched for this address and neither do we — same as a password sign-up.
        user.EmailConfirmed.ShouldBeFalse();
    }

    [Fact]
    public async Task An_Emailless_Identity_Cannot_Take_Over_An_Existing_Account_By_Typing_Its_Email()
    {
        var client = _factory!.CreateClient();
        await RegisterAsync(client, "victim@example.com");

        var identity = new LinkedInIdentity("li-noemail-attacker", null, false, "Eve", "Attacker");
        var pending = (await (await SignInAsync(client, identity))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;

        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "Eve", "Attacker", "victim@example.com", true), JsonOptions);

        // Rejected as an ordinary duplicate email — never silently linked to the victim's account.
        signup.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserLogins.AnyAsync(l => l.ProviderKey == "li-noemail-attacker")).ShouldBeFalse();
    }

    [Fact]
    public async Task An_Unverified_Email_Is_Dropped_And_Behaves_Exactly_Like_No_Email_At_All()
    {
        var client = _factory!.CreateClient();
        var identity = new LinkedInIdentity("li-unverified", "unverified@example.com", false, "U", "V");

        var pending = (await (await SignInAsync(client, identity))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;

        // The prefill drops the unverified address entirely — the form must collect one explicitly.
        pending.Email.ShouldBeNull();

        var signup = await client.PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "U", "V", "unverified@example.com", true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task The_Same_Emailless_LinkedIn_Subject_Signs_Straight_In_On_A_Second_Visit()
    {
        var identity = new LinkedInIdentity("li-noemail-return", null, false, "Return", "User");

        var pending = (await (await SignInAsync(_factory!.CreateClient(), identity))
            .Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!.PendingSignup!;
        var signup = await _factory.CreateClient().PostAsJsonAsync("/api/auth/linkedin/signup",
            new LinkedInSignupRequest(pending.SignupToken, "Return", "User", "return.user@example.com", true), JsonOptions);
        var firstUserId = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!.User.Id;

        // Still no email from LinkedIn on the second visit — the subject alone is enough to recognise
        // the account; it must not be sent through the sign-up step again.
        var again = await SignInAsync(_factory.CreateClient(), identity);
        var againBody = (await again.Content.ReadFromJsonAsync<LinkedInSignInResponse>(JsonOptions))!;
        againBody.PendingSignup.ShouldBeNull();
        againBody.Auth!.User.Id.ShouldBe(firstUserId);
    }

    private Task<HttpResponseMessage> SignInAsync(HttpClient client, LinkedInIdentity identity) =>
        client.PostAsJsonAsync("/api/auth/linkedin",
            new LinkedInSignInRequest(_linkedIn.IssueCode(identity), RedirectUri), JsonOptions);

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, Password, "Pass", "Word", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client, string? password)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password), options: JsonOptions)
        };
        return client.SendAsync(request);
    }
}
