using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
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
/// <summary>The test host runs as Development and therefore loads the developer's user-secrets,
/// where real OAuth client ids/secrets may well be set (they are on the machine this was written
/// on). Cleared so "not configured" means exactly that, everywhere.</summary>
public sealed class ClientConfigProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("GoogleAuth:ClientId", "");
        builder.UseSetting("GoogleAuth:ClientSecret", "");
        builder.UseSetting("LinkedInAuth:ClientId", "");
        builder.UseSetting("LinkedInAuth:ClientSecret", "");
        builder.UseSetting("GitHubAuth:ClientId", "");
        builder.UseSetting("GitHubAuth:ClientSecret", "");
    }
}

[Collection(IntegrationTestCollection.Name)]
public class ClientConfigTests(ApiHost<ClientConfigProfile> host) : IClassFixture<ApiHost<ClientConfigProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _defaultFactory => host;

    private WebApplicationFactory<Program> _overriddenFactory => host.Variant("overridden", builder =>
    {
        builder.UseSetting("Identity:Password:RequiredLength", "20");
        builder.UseSetting("Identity:Password:RequireNonAlphanumeric", "false");
        builder.UseSetting("PersonalAccessTokens:MaxActiveTokens", "3");
        builder.UseSetting("PersonalAccessTokens:LifetimeDays", "7");
    });

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Config_Is_Anonymous_And_Publishes_The_Default_Policy()
    {
        var client = _defaultFactory.CreateClient();

        var response = await client.GetAsync("/api/config");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        // Public cache + CORS must vary on Origin, or an address-bar visit poisons the cache for
        // the web app's cross-origin fetch — see ClientConfigEndpoints.
        response.Headers.Vary.ShouldContain("Origin");
        var config = await response.Content.ReadFromJsonAsync<ClientConfigResponse>(JsonOptions);
        config.ShouldNotBeNull();
        // Length over composition (NIST SP 800-63B): 12 characters, 4 distinct, no character-class rules.
        config.PasswordPolicy.ShouldBe(new PasswordPolicyResponse(12, 4, false, false, false, false));
        config.PersonalAccessTokens.ShouldBe(new PersonalAccessTokenLimitsResponse(10, 90));
        // No GoogleAuth section set: the feature is reported off and no client id leaks out.
        config.GoogleAuth.ShouldBe(new GoogleAuthConfigResponse(false, null));
        config.LinkedInAuth.ShouldBe(new LinkedInAuthConfigResponse(false, null));
        config.GitHubAuth.ShouldBe(new GitHubAuthConfigResponse(false, null));
        // The candidate-experience slot carries the appsettings defaults: on, ten per account, three for stats.
        config.CandidateExperiences.ShouldBe(new CandidateExperiencesConfigResponse(true, 10, 3, 5));
    }

    [Fact]
    public async Task Overridden_Policy_Is_Both_Published_And_Enforced()
    {
        var client = _overriddenFactory.CreateClient();

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.PasswordPolicy.RequiredLength.ShouldBe(20);
        config.PasswordPolicy.RequireNonAlphanumeric.ShouldBeFalse();
        config.PersonalAccessTokens.ShouldBe(new PersonalAccessTokenLimitsResponse(3, 7));

        // Valid under the default policy (12 chars), too short under the override.
        var tooShort = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("policy.short@example.com", "P@ssw0rd123!", "Policy", "Test", true), JsonOptions);
        tooShort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await tooShort.Content.ReadAsStringAsync();
        problem.ShouldContain("20");

        // 20 chars, no special character — accepted with the override.
        var accepted = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("policy.long@example.com", "LongPassword12345678", "Policy", "Test", true), JsonOptions);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Length over composition (2026-09-14): a twelve-character passphrase with no digit, no
    /// upper-case letter and no symbol is a valid password. What the default policy still refuses
    /// is a short one and one made of a few repeated characters.
    /// </summary>
    [Theory]
    [InlineData("correct horse battery", HttpStatusCode.Created)]
    [InlineData("kelimelerbirarada", HttpStatusCode.Created)]
    [InlineData("kisa sifre", HttpStatusCode.BadRequest)]
    [InlineData("aaaaaaaaaaaa", HttpStatusCode.BadRequest)]
    public async Task Default_Policy_Judges_A_Password_By_Its_Length_Not_Its_Character_Classes(string password, HttpStatusCode expected)
    {
        var client = _defaultFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"policy.{Guid.NewGuid():N}@example.com", password, "Policy", "Test", true), JsonOptions);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Overridden_Token_Limit_Is_Enforced_And_Quoted_In_The_Error()
    {
        var client = _overriddenFactory.CreateClient();
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
