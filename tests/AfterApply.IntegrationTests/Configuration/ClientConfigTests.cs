using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Configuration;

/// <summary>
/// The password/lockout policy and the personal-access-token limits are read from configuration
/// (2026-09-05) so that they can be retuned without a redeploy. These tests pin two things: the
/// defaults are the pre-existing hardcoded values, and an override actually reaches both the
/// published config and the validator that enforces it — the two must never disagree, because the
/// web app shows the user the former and the server rejects on the latter.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ClientConfigTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _defaultFactory;
    private WebApplicationFactory<Program>? _overriddenFactory;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(ClientConfigTests));

        _defaultFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _overriddenFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Identity:Password:RequiredLength", "20");
            builder.UseSetting("Identity:Password:RequireNonAlphanumeric", "false");
            builder.UseSetting("PersonalAccessTokens:MaxActiveTokens", "3");
            builder.UseSetting("PersonalAccessTokens:LifetimeDays", "7");
        });
    }

    public async Task DisposeAsync()
    {
        if (_defaultFactory is not null)
        {
            await _defaultFactory.DisposeAsync();
        }

        if (_overriddenFactory is not null)
        {
            await _overriddenFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Config_Is_Anonymous_And_Publishes_The_Default_Policy()
    {
        var client = _defaultFactory!.CreateClient();

        var response = await client.GetAsync("/api/config");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        var config = await response.Content.ReadFromJsonAsync<ClientConfigResponse>(JsonOptions);
        config.ShouldNotBeNull();
        config.PasswordPolicy.ShouldBe(new PasswordPolicyResponse(12, 4, true, true, true, true));
        config.PersonalAccessTokens.ShouldBe(new PersonalAccessTokenLimitsResponse(10, 90));
    }

    [Fact]
    public async Task Overridden_Policy_Is_Both_Published_And_Enforced()
    {
        var client = _overriddenFactory!.CreateClient();

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.PasswordPolicy.RequiredLength.ShouldBe(20);
        config.PasswordPolicy.RequireNonAlphanumeric.ShouldBeFalse();
        config.PersonalAccessTokens.ShouldBe(new PersonalAccessTokenLimitsResponse(3, 7));

        // Valid under the default policy (12 chars, every class present), too short under the override.
        var tooShort = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("policy.short@example.com", "P@ssw0rd123!", "Policy", "Test", true), JsonOptions);
        tooShort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await tooShort.Content.ReadAsStringAsync();
        problem.ShouldContain("20");

        // 20 chars, no special character — rejected by default, accepted with the override.
        var accepted = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("policy.long@example.com", "LongPassword12345678", "Policy", "Test", true), JsonOptions);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Overridden_Token_Limit_Is_Enforced_And_Quoted_In_The_Error()
    {
        var client = _overriddenFactory!.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("policy.tokens@example.com", "LongPassword12345678", "Policy", "Test", true), JsonOptions);
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        for (var i = 0; i < 3; i++)
        {
            var created = await client.PostAsJsonAsync("/api/personal-access-tokens",
                new CreatePersonalAccessTokenRequest($"token-{i}"), JsonOptions);
            created.StatusCode.ShouldBe(HttpStatusCode.OK);
            var token = await created.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions);
            (token!.ExpiresAt - token.CreatedAt).TotalDays.ShouldBe(7, tolerance: 0.01);
        }

        var fourth = await client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("token-3"), JsonOptions);
        fourth.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = await fourth.Content.ReadAsStringAsync();
        detail.ShouldContain("3");
        detail.ShouldNotContain("{0}");
    }
}
