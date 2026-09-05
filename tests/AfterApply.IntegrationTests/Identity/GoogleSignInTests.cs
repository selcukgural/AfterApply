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
/// Sign in with Google, end to end against the real host — everything except the round-trip to
/// Google, which FakeGoogleAuthClient replaces with "this code means this identity". Covers the
/// three outcomes of POST /api/auth/google (already linked → signed in; verified email matches an
/// account → linked and signed in; unknown → sign-up step), the consent gate on /google/signup, the
/// rejections, and what a password-less account means for the rest of the API.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class GoogleSignInTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string ClientId = "test-client.apps.googleusercontent.com";
    private const string RedirectUri = "http://localhost:3000/tr/auth/google/callback";
    private const string CodeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Password = "P@ssw0rd123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FakeGoogleAuthClient _google = new();
    private WebApplicationFactory<Program>? _factory;
    private WebApplicationFactory<Program>? _unconfiguredFactory;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(GoogleSignInTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("App:WebBaseUrl", "http://localhost:3000");
            builder.UseSetting("GoogleAuth:ClientId", ClientId);
            builder.UseSetting("GoogleAuth:ClientSecret", "test-secret");
            builder.ConfigureTestServices(services => services.AddSingleton<IGoogleAuthClient>(_google));
        });

        // Same database, no Google client configured — the shape of a deployment that never set
        // the two secrets.
        _unconfiguredFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // The test host runs as Development and therefore loads the developer's user-secrets,
            // where a real GoogleAuth client id/secret may well be set (it is on the machine this
            // was written on). Clear them so "not configured" means exactly that, everywhere.
            builder.UseSetting("GoogleAuth:ClientId", "");
            builder.UseSetting("GoogleAuth:ClientSecret", "");
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
        configured!.GoogleAuth.ShouldBe(new GoogleAuthConfigResponse(true, ClientId));

        var unconfigured = await _unconfiguredFactory!.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        unconfigured!.GoogleAuth.ShouldBe(new GoogleAuthConfigResponse(false, null));
    }

    [Fact]
    public async Task Both_Endpoints_Are_404_When_Not_Configured()
    {
        var client = _unconfiguredFactory!.CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/auth/google",
            new GoogleSignInRequest("code", CodeVerifier, RedirectUri), JsonOptions);
        signIn.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var signup = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest("token", "Ada", "Lovelace", true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_New_Google_Account_Gets_A_Signup_Step_And_Is_Created_Only_With_Consent()
    {
        var client = _factory!.CreateClient();
        var identity = new GoogleIdentity("g-new-1", "new.google@example.com", true, "Ada", "Lovelace");

        var signIn = await SignInAsync(client, identity);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await signIn.Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!;
        body.Auth.ShouldBeNull();
        body.PendingSignup.ShouldNotBeNull();
        body.PendingSignup.Email.ShouldBe("new.google@example.com");
        body.PendingSignup.FirstName.ShouldBe("Ada");
        body.PendingSignup.LastName.ShouldBe("Lovelace");
        // Nothing was created yet.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.AnyAsync(u => u.Email == "new.google@example.com")).ShouldBeFalse();
        }

        var withoutConsent = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(body.PendingSignup.SignupToken, "Ada", "Lovelace", ConsentAccepted: false), JsonOptions);
        withoutConsent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The user edited the pre-filled name before accepting — what they confirmed is what's stored.
        var signup = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(body.PendingSignup.SignupToken, "Augusta Ada", "King", ConsentAccepted: true), JsonOptions);
        signup.StatusCode.ShouldBe(HttpStatusCode.Created);
        var auth = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
        auth.User.Email.ShouldBe("new.google@example.com");
        auth.User.FirstName.ShouldBe("Augusta Ada");
        auth.User.LastName.ShouldBe("King");
        auth.User.HasPassword.ShouldBeFalse();
        auth.User.ConsentAcceptedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));

        // The tokens are real: the protected profile endpoint accepts them.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        me!.Id.ShouldBe(auth.User.Id);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
            user.EmailConfirmed.ShouldBeTrue();
            user.PasswordHash.ShouldBeNull();
            (await db.UserLogins.SingleAsync(l => l.UserId == user.Id)).ProviderKey.ShouldBe("g-new-1");
        }

        // From now on the same Google account signs straight in — no sign-up step, same user.
        var again = await SignInAsync(_factory.CreateClient(), identity);
        var againBody = (await again.Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!;
        againBody.PendingSignup.ShouldBeNull();
        againBody.Auth!.User.Id.ShouldBe(auth.User.Id);
    }

    [Fact]
    public async Task Replaying_The_Signup_Token_After_The_Account_Exists_Signs_In_Instead_Of_Duplicating()
    {
        var client = _factory!.CreateClient();
        var identity = new GoogleIdentity("g-replay", "replay@example.com", true, "R", "E");

        var pending = (await (await SignInAsync(client, identity)).Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!.PendingSignup!;
        var first = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(pending.SignupToken, "R", "E", true), JsonOptions);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(pending.SignupToken, "R", "E", true), JsonOptions);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        var firstUser = (await first.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!.User.Id;
        var secondUser = (await second.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!.User.Id;
        secondUser.ShouldBe(firstUser);
    }

    [Fact]
    public async Task A_Verified_Email_Matching_A_Password_Account_Is_Linked_And_Signed_In()
    {
        var client = _factory!.CreateClient();
        var registered = await RegisterAsync(client, "linked@example.com");
        registered.User.HasPassword.ShouldBeTrue();

        var signIn = await SignInAsync(client, new GoogleIdentity("g-linked", "Linked@Example.com", true, "L", "K"));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await signIn.Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!;
        body.PendingSignup.ShouldBeNull();
        body.Auth!.User.Id.ShouldBe(registered.User.Id);
        // Linking never strips the password: the account is now reachable both ways.
        body.Auth.User.HasPassword.ShouldBeTrue();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("linked@example.com", Password), JsonOptions);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == registered.User.Id);
        user.EmailConfirmed.ShouldBeTrue();
        (await db.UserLogins.CountAsync(l => l.UserId == user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task An_Unverified_Google_Email_Is_Rejected()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");

        var signIn = await SignInAsync(client, new GoogleIdentity("g-unverified", "unverified@example.com", false, null, null));

        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await signIn.Content.ReadAsStringAsync()).ShouldContain("not verified");
    }

    [Fact]
    public async Task A_Code_Google_Does_Not_Recognise_Is_Rejected()
    {
        var client = _factory!.CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/auth/google",
            new GoogleSignInRequest("4/not-issued", CodeVerifier, RedirectUri), JsonOptions);

        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Redirect_Uri_Outside_Our_Web_Origin_Is_Rejected_Before_Reaching_Google()
    {
        var client = _factory!.CreateClient();
        var code = _google.IssueCode(new GoogleIdentity("g-redirect", "redirect@example.com", true, null, null));

        var signIn = await client.PostAsJsonAsync("/api/auth/google",
            new GoogleSignInRequest(code, CodeVerifier, "https://evil.example.com/tr/auth/google/callback"), JsonOptions);

        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _google.Exchanges.ShouldNotContain(e => e.Code == code);
    }

    [Fact]
    public async Task A_Tampered_Or_Foreign_Signup_Token_Is_A_Validation_Problem()
    {
        var client = _factory!.CreateClient();
        var registered = await RegisterAsync(client, "foreign.token@example.com");

        // One of our own access tokens: same signing key, wrong audience/purpose.
        var withAccessToken = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(registered.AccessToken, "A", "B", true), JsonOptions);
        withAccessToken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var withGarbage = await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest("garbage", "A", "B", true), JsonOptions);
        withGarbage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Google_Only_Account_Is_Deleted_Without_A_Password_But_A_Password_Account_Still_Needs_One()
    {
        var googleClient = _factory!.CreateClient();
        var pending = (await (await SignInAsync(googleClient, new GoogleIdentity("g-delete", "delete.google@example.com", true, "D", "G")))
            .Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!.PendingSignup!;
        var signup = await googleClient.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(pending.SignupToken, "D", "G", true), JsonOptions);
        var googleAuth = (await signup.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
        googleClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", googleAuth.AccessToken);

        (await DeleteAccountAsync(googleClient, password: null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var passwordClient = _factory.CreateClient();
        var registered = await RegisterAsync(passwordClient, "delete.password@example.com");
        passwordClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);

        (await DeleteAccountAsync(passwordClient, password: null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DeleteAccountAsync(passwordClient, Password)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Password_Login_For_A_Google_Only_Account_Fails_Like_Any_Wrong_Password()
    {
        var client = _factory!.CreateClient();
        var pending = (await (await SignInAsync(client, new GoogleIdentity("g-nopass", "nopass@example.com", true, "N", "P")))
            .Content.ReadFromJsonAsync<GoogleSignInResponse>(JsonOptions))!.PendingSignup!;
        (await client.PostAsJsonAsync("/api/auth/google/signup",
            new GoogleSignupRequest(pending.SignupToken, "N", "P", true), JsonOptions)).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nopass@example.com", Password), JsonOptions);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> SignInAsync(HttpClient client, GoogleIdentity identity) =>
        client.PostAsJsonAsync("/api/auth/google",
            new GoogleSignInRequest(_google.IssueCode(identity), CodeVerifier, RedirectUri), JsonOptions);

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
