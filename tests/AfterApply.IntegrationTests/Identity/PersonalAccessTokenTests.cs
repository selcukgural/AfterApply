using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AfterApply.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Identity;

// Covers the Sprint 9 Personal Access Token flow end to end: create/list/revoke via the
// JWT-authenticated web session, and — the part that actually exercises the "SmartBearer" policy
// scheme wiring in DependencyInjection.AddIdentityAndJwt — using the raw PAT value on its own,
// with NO JWT anywhere in the request, to authenticate against an ordinary RequireAuthorization()
// endpoint (GET /api/applications). A revoked token must stop working immediately.
public class PersonalAccessTokenTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("pat.test@example.com", "P@ssw0rd123!", "Pat", "Test", true), JsonOptions);
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

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

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
    public async Task Raw_Token_Alone_Authenticates_Against_An_Ordinary_Protected_Endpoint()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension"), JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions);

        using var patOnlyClient = _factory!.CreateClient();
        patOnlyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created!.Token);

        var response = await patOnlyClient.GetAsync("/api/applications");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoked_Token_No_Longer_Authenticates()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension"), JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions);

        var revokeResponse = await _client.DeleteAsync($"/api/personal-access-tokens/{created!.Id}");
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var patOnlyClient = _factory!.CreateClient();
        patOnlyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);

        var response = await patOnlyClient.GetAsync("/api/applications");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
