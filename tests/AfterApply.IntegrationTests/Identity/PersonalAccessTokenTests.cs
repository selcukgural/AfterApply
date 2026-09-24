using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AfterApply.Infrastructure.Persistence;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

// Covers the Sprint 9 Personal Access Token flow end to end: create/list/revoke via the
// JWT-authenticated web session, and — the part that actually exercises the "SmartBearer" policy
// scheme wiring in DependencyInjection.AddIdentityAndJwt — using the raw PAT value on its own,
// with NO JWT anywhere in the request, to authenticate against an ordinary RequireAuthorization()
// endpoint (GET /api/applications). A revoked token must stop working immediately.
//
// Since the 2026-09-03 security pass, tokens also carry a scope, and since 2026-09-24 every token is
// held to the extension's endpoints: Full can no longer be issued, and a Full token issued before
// reaches no more than an Extension one. The tests that just need "a working credential" therefore
// use GET /api/companies/search, one of those endpoints. The scope boundary itself is covered by
// its own tests below (allowed endpoint → 200, everything else → 403).
[Collection(IntegrationTestCollection.Name)]
public class PersonalAccessTokenTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        (_client, _) = await host.RegisterAsync("pat.test@example.com", "Pat", "Test");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_Returns_Raw_Token_Once_And_List_Never_Includes_It()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension"), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions);
        created!.Token.ShouldStartWith("aa_pat_");

        var listResponse = await _client.GetAsync("/api/personal-access-tokens");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<PersonalAccessTokenResponse>>(JsonOptions);
        list!.ShouldContain(t => t.Id == created.Id && t.Name == "Chrome Extension");
        (await listResponse.Content.ReadAsStringAsync()).ShouldNotContain(created.Token);
    }

    [Fact]
    public async Task Raw_Token_Alone_Authenticates_Against_A_Protected_Endpoint()
    {
        var created = await CreateTokenAsync("Chrome Extension");

        using var patOnlyClient = CreatePatOnlyClient(created.Token);

        var response = await patOnlyClient.GetAsync("/api/companies/search?q=acme");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoked_Token_No_Longer_Authenticates()
    {
        var created = await CreateTokenAsync("Chrome Extension");

        var revokeResponse = await _client.DeleteAsync($"/api/personal-access-tokens/{created.Id}");
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var patOnlyClient = CreatePatOnlyClient(created.Token);

        var response = await patOnlyClient.GetAsync("/api/companies/search?q=acme");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Defaults_To_Extension_Scope_And_A_90_Day_Expiry()
    {
        var created = await CreateTokenAsync("Chrome Extension");

        created.Scope.ShouldBe(PersonalAccessTokenScope.Extension);
        // Not an exact equality check — CreatedAt is stamped server-side, so this only asserts the
        // window is the intended 90 days rather than the scaffolded default the migration replaced.
        (created.ExpiresAt - created.CreatedAt).TotalDays.ShouldBe(90, tolerance: 0.01);
    }

    [Fact]
    public async Task Extension_Scoped_Token_Reaches_The_Endpoints_The_Extension_Calls()
    {
        var created = await CreateTokenAsync("Chrome Extension", PersonalAccessTokenScope.Extension);

        using var patOnlyClient = CreatePatOnlyClient(created.Token);

        var response = await patOnlyClient.GetAsync("/api/companies/search?q=acme");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The point of the scope: a token leaked out of chrome.storage.local must not be able to walk
    // off with the account's whole application history. 403, not 401 — the credential is valid,
    // it just isn't allowed here.
    [Theory]
    [InlineData("/api/applications")]
    [InlineData("/api/users/me")]
    [InlineData("/api/users/me/export")]
    [InlineData("/api/users/me/plan")]
    [InlineData("/api/personal-access-tokens")]
    public async Task Extension_Scoped_Token_Is_Forbidden_Everywhere_Else(string path)
    {
        var created = await CreateTokenAsync("Chrome Extension", PersonalAccessTokenScope.Extension);

        using var patOnlyClient = CreatePatOnlyClient(created.Token);

        var response = await patOnlyClient.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // A token outlives the session that minted it by 90 days; a session-equivalent one would turn
    // twenty minutes with a stolen access token into three months with the whole account.
    [Fact]
    public async Task A_Full_Scoped_Token_Cannot_Be_Created()
    {
        var response = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Scripting", PersonalAccessTokenScope.Full), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.WithDbAsync(db => db.PersonalAccessTokens.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task A_Scope_The_Enum_Does_Not_Know_Is_Rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/personal-access-tokens", new { name = "Odd", scope = 7 }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Full tokens issued before 2026-09-24 still exist; they keep working for the extension and
    // reach nothing else — token management and export included.
    [Theory]
    [InlineData("/api/applications")]
    [InlineData("/api/users/me/export")]
    [InlineData("/api/personal-access-tokens")]
    public async Task A_Full_Token_Issued_Before_Is_Held_To_The_Extension_Endpoints(string path)
    {
        var created = await CreateTokenAsync("Old scripting token");
        // Before the token's first use, so no cached validation carries the old scope.
        await host.WithDbAsync(db => db.PersonalAccessTokens.Where(t => t.Id == created.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Scope, PersonalAccessTokenScope.Full)));

        using var patOnlyClient = CreatePatOnlyClient(created.Token);

        (await patOnlyClient.GetAsync("/api/companies/search?q=acme")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patOnlyClient.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Token_Cannot_Mint_Another_Token()
    {
        var created = await CreateTokenAsync("Chrome Extension");

        using var patOnlyClient = CreatePatOnlyClient(created.Token);
        var response = await patOnlyClient.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Second"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<CreatedPersonalAccessTokenResponse> CreateTokenAsync(
        string name, PersonalAccessTokenScope? scope = null)
    {
        var request = scope is null
            ? new CreatePersonalAccessTokenRequest(name)
            : new CreatePersonalAccessTokenRequest(name, scope.Value);

        var response = await _client.PostAsJsonAsync("/api/personal-access-tokens", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions))!;
    }

    private HttpClient CreatePatOnlyClient(string rawToken)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        return client;
    }

    [Fact]
    public async Task Create_Fails_After_10_Active_Tokens()
    {
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/personal-access-tokens",
                new CreatePersonalAccessTokenRequest($"Token {i}"), JsonOptions);
            response.EnsureSuccessStatusCode();
        }

        var eleventhResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Token 11"), JsonOptions);
        eleventhResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await eleventhResponse.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions);
        var detail = problem!.RootElement.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("10");
    }

    [Fact]
    public async Task Revoking_A_Token_Frees_A_Slot_At_The_Limit()
    {
        var created = new List<CreatedPersonalAccessTokenResponse>();
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/personal-access-tokens",
                new CreatePersonalAccessTokenRequest($"Token {i}"), JsonOptions);
            response.EnsureSuccessStatusCode();
            created.Add((await response.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions))!);
        }

        var revokeResponse = await _client.DeleteAsync($"/api/personal-access-tokens/{created[0].Id}");
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterRevokeResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Token 11"), JsonOptions);
        afterRevokeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
