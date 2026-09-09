using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>
/// The pairing handshake that replaced "generate a key, copy it, paste it into the extension".
///
/// The flow has two callers and the tests keep them apart the way the real thing does: an
/// anonymous client with no credential at all (the extension) starts and polls, and a signed-in
/// browser session confirms. What is worth pinning down is mostly what the flow must *not* do —
/// hand a token to a poll that was never confirmed, hand one out twice, let an extension-scoped
/// token confirm its own successor, or accept a code after it has expired.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ExtensionPairingTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;

    /// <summary>The signed-in browser session that confirms a pairing.</summary>
    private HttpClient _client = null!;

    /// <summary>The extension: no Authorization header anywhere, ever.</summary>
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(ExtensionPairingTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("App:WebBaseUrl", "https://ekariyerim.example");
        });

        _client = _factory.CreateClient();
        _anonymous = _factory.CreateClient();

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("pairing.test@example.com", "P@ssw0rd123!", "Pairing", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Start_Answers_An_Anonymous_Caller_With_A_Code_A_Secret_And_A_Url()
    {
        var started = await StartAsync();

        started.Code.Length.ShouldBe(ExtensionPairingCode.Length);
        started.DeviceSecret.ShouldNotBeNullOrWhiteSpace();
        // The two halves are different things: one is read by a person, the other is the only way
        // to collect the token. A response that returned the same value twice would look fine and
        // would have thrown the whole design away.
        started.DeviceSecret.ShouldNotBe(started.Code);
        started.VerificationUrl.ShouldBe($"https://ekariyerim.example/tr/pair?code={started.Code}");
        started.PollIntervalSeconds.ShouldBeGreaterThan(0);
        started.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Start_Puts_The_Requested_Locale_In_The_Verification_Url()
    {
        var started = await StartAsync("en");

        started.VerificationUrl.ShouldStartWith("https://ekariyerim.example/en/pair?code=");
    }

    [Fact]
    public async Task Start_Refuses_A_Locale_The_Site_Does_Not_Have()
    {
        // The locale is interpolated into a URL. Anything not on the list is rejected rather than
        // pasted in.
        var response = await _anonymous.PostAsJsonAsync("/api/extension-pairing/requests",
            new StartExtensionPairingRequest("../../evil.example"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Poll_Says_Pending_Until_Somebody_Confirms_And_Hands_Over_No_Token()
    {
        var started = await StartAsync();

        var poll = await PollAsync(started.DeviceSecret);

        poll.Status.ShouldBe(ExtensionPairingStatus.Pending);
        poll.Token.ShouldBeNull();
    }

    [Fact]
    public async Task Approving_Then_Polling_Hands_Over_A_Token_Exactly_Once()
    {
        var started = await StartAsync();

        var approved = await ApproveAsync(started.Code);
        approved.Status.ShouldBe(ExtensionPairingStatus.Approved);

        var first = await PollAsync(started.DeviceSecret);
        first.Status.ShouldBe(ExtensionPairingStatus.Completed);
        first.Token.ShouldNotBeNull();
        first.Token.ShouldStartWith("aa_pat_");
        first.TokenExpiresAt.ShouldNotBeNull();

        // A second poll — an options page reloaded mid-flow — must not mint a second credential.
        var second = await PollAsync(started.DeviceSecret);
        second.Status.ShouldBe(ExtensionPairingStatus.Completed);
        second.Token.ShouldBeNull();
    }

    [Fact]
    public async Task The_Token_A_Pairing_Produces_Is_Extension_Scoped_And_Belongs_To_The_Approver()
    {
        var started = await StartAsync();
        await ApproveAsync(started.Code);
        var poll = await PollAsync(started.DeviceSecret);

        using var extension = _factory!.CreateClient();
        extension.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", poll.Token);

        // Reaches what the extension actually calls...
        (await extension.GetAsync("/api/companies/search?q=acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        // ...and nothing else. A token sitting in chrome.storage.local must not be able to walk off
        // with the account's history.
        (await extension.GetAsync("/api/users/me/export")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // And it is the approver's token — it shows up in their own list, under a recognisable name.
        var list = await _client.GetFromJsonAsync<List<PersonalAccessTokenResponse>>(
            "/api/personal-access-tokens", JsonOptions);
        list.ShouldNotBeNull();
        list.ShouldContain(token => token.Scope == PersonalAccessTokenScope.Extension && token.Name == "Browser Extension");
    }

    [Fact]
    public async Task A_Code_Is_Matched_However_The_User_Typed_It()
    {
        var started = await StartAsync();

        var lowerCasedAndDashed = $"{started.Code[..4].ToLowerInvariant()}-{started.Code[4..].ToLowerInvariant()}";
        var approved = await ApproveAsync(lowerCasedAndDashed);

        approved.Status.ShouldBe(ExtensionPairingStatus.Approved);
    }

    [Fact]
    public async Task Denying_Is_Terminal_And_Produces_No_Token()
    {
        var started = await StartAsync();

        var denied = await _client.PostAsync($"/api/extension-pairing/requests/{started.Code}/deny", null);
        denied.EnsureSuccessStatusCode();

        var poll = await PollAsync(started.DeviceSecret);
        poll.Status.ShouldBe(ExtensionPairingStatus.Denied);
        poll.Token.ShouldBeNull();

        // And it cannot be talked out of it afterwards.
        var approved = await ApproveAsync(started.Code);
        approved.Status.ShouldBe(ExtensionPairingStatus.Denied);
    }

    [Fact]
    public async Task An_Expired_Request_Is_Neither_Approvable_Nor_Collectable()
    {
        var started = await StartAsync();
        await ExpireAsync(started.Code);

        var approved = await ApproveAsync(started.Code);
        approved.Status.ShouldBe(ExtensionPairingStatus.Expired);

        var poll = await PollAsync(started.DeviceSecret);
        poll.Status.ShouldBe(ExtensionPairingStatus.Expired);
        poll.Token.ShouldBeNull();
    }

    [Fact]
    public async Task An_Approval_Left_Uncollected_Past_The_Deadline_Mints_Nothing()
    {
        var started = await StartAsync();
        await ApproveAsync(started.Code);
        await ExpireAsync(started.Code);

        var poll = await PollAsync(started.DeviceSecret);

        // The approval does not become a standing permission to mint a token whenever.
        poll.Status.ShouldBe(ExtensionPairingStatus.Expired);
        poll.Token.ShouldBeNull();
    }

    [Fact]
    public async Task Polling_With_An_Unknown_Secret_Is_A_404()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/extension-pairing/poll",
            new PollExtensionPairingRequest("not-a-real-device-secret"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reading_Or_Approving_An_Unknown_Code_Is_A_404()
    {
        (await _client.GetAsync("/api/extension-pairing/requests/ZZZZZZZZ")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostAsync("/api/extension-pairing/requests/ZZZZZZZZ/approve", null)).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Confirming_Requires_A_Signed_In_Session()
    {
        var started = await StartAsync();

        var response = await _anonymous.PostAsync($"/api/extension-pairing/requests/{started.Code}/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Extension_Token_Cannot_Confirm_Its_Own_Successor()
    {
        var created = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension", PersonalAccessTokenScope.Extension), JsonOptions);
        created.EnsureSuccessStatusCode();
        var token = await created.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions);

        var started = await StartAsync();

        using var extension = _factory!.CreateClient();
        extension.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);

        // 403, not 401: the credential is valid, it just has no business confirming a pairing —
        // otherwise a leaked extension token could keep renewing itself into a fresh one forever.
        var response = await extension.PostAsync($"/api/extension-pairing/requests/{started.Code}/approve", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_Review_Endpoint_Shows_The_Code_And_Its_Deadline_But_Never_The_Secret()
    {
        var started = await StartAsync();

        var response = await _client.GetAsync($"/api/extension-pairing/requests/{started.Code}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(started.DeviceSecret);

        var review = JsonSerializer.Deserialize<ExtensionPairingReviewResponse>(body, JsonOptions);
        review!.Code.ShouldBe(started.Code);
        review.Status.ShouldBe(ExtensionPairingStatus.Pending);
        review.ExpiresAt.ShouldBe(started.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task No_Raw_Secret_Is_Stored_Alongside_The_Request()
    {
        var started = await StartAsync();

        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.ExtensionPairingRequests.SingleAsync(r => r.Code == started.Code);

        stored.DeviceSecretHash.ShouldNotBe(started.DeviceSecret);
        stored.CompletedAt.ShouldBeNull();
        stored.ApprovedByUserId.ShouldBeNull();
    }

    private async Task<StartedExtensionPairingResponse> StartAsync(string? locale = null)
    {
        var response = await _anonymous.PostAsJsonAsync("/api/extension-pairing/requests",
            new StartExtensionPairingRequest(locale), JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StartedExtensionPairingResponse>(JsonOptions))!;
    }

    private async Task<ExtensionPairingPollResponse> PollAsync(string deviceSecret)
    {
        var response = await _anonymous.PostAsJsonAsync("/api/extension-pairing/poll",
            new PollExtensionPairingRequest(deviceSecret), JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExtensionPairingPollResponse>(JsonOptions))!;
    }

    private async Task<ExtensionPairingReviewStatus> ApproveAsync(string code)
    {
        var response = await _client.PostAsync($"/api/extension-pairing/requests/{code}/approve", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExtensionPairingReviewStatus>(JsonOptions))!;
    }

    /// <summary>Moves a request's deadline into the past. The lifetime is ten minutes and the
    /// service reads the wall clock, so this is the only way to reach the expiry paths without
    /// making the suite wait.</summary>
    private async Task ExpireAsync(string code)
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.ExtensionPairingRequests
            .Where(r => r.Code == code)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private sealed record ExtensionPairingReviewStatus(ExtensionPairingStatus Status);
}
