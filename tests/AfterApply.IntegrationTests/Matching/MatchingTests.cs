using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Matching;
using AfterApply.Application.Matching.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Matching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Matching;

// No real OpenAI API key is used here — a FakeJobMatchingProvider is registered in place of the
// real OpenAiJobMatchingProvider (same approach as EmailIntegrationTests/FakeGmailClient for
// Phase 9). Covers the profile CRUD, the cache/recompute decision in JobMatchingService, and
// ownership/validation error paths.
public class MatchingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private readonly FakeJobMatchingProvider _fakeProvider = new();
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
            // Flag defaults to false in appsettings.json (hidden pending KVKK consent work, see
            // MatchingEndpoints) — flipped on here since this class exercises the feature itself,
            // same pattern as CompanyIntelligenceTests' _enabledFactory.
            builder.UseSetting("Matching:Enabled", "true");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IJobMatchingProvider>(_fakeProvider);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("matching.test@example.com", "P@ssw0rd123!", "Matching", "Test", true), JsonOptions);
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

    private async Task<Guid> CreateApplicationAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Acme Corp", "Backend Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-5), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    [Fact]
    public async Task Profile_Roundtrips_Through_Put_And_Get()
    {
        var getBeforeResponse = await _client.GetAsync("/api/matching/profile");
        getBeforeResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var putResponse = await _client.PutAsJsonAsync("/api/matching/profile",
            new UpdateCandidateProfileRequest("C# / .NET / PostgreSQL"), JsonOptions);
        putResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/api/matching/profile");
        getResponse.EnsureSuccessStatusCode();
        var profile = await getResponse.Content.ReadFromJsonAsync<CandidateProfileResponse>(JsonOptions);
        profile!.CvText.ShouldBe("C# / .NET / PostgreSQL");
    }

    [Fact]
    public async Task ComputeMatch_Without_Profile_Returns_BadRequest()
    {
        var applicationId = await CreateApplicationAsync();

        var response = await _client.PostAsJsonAsync($"/api/matching/applications/{applicationId}",
            new ComputeJobMatchRequest("We need a C# backend engineer."), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ComputeMatch_For_Foreign_Or_Unknown_Application_Returns_NotFound()
    {
        await _client.PutAsJsonAsync("/api/matching/profile",
            new UpdateCandidateProfileRequest("C# / .NET"), JsonOptions);

        var response = await _client.PostAsJsonAsync($"/api/matching/applications/{Guid.CreateVersion7()}",
            new ComputeJobMatchRequest("We need a C# backend engineer."), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ComputeMatch_Persists_Result_And_Serves_Cache_On_Repeat_Request()
    {
        await _client.PutAsJsonAsync("/api/matching/profile",
            new UpdateCandidateProfileRequest("C# / .NET / PostgreSQL"), JsonOptions);
        var applicationId = await CreateApplicationAsync();
        var jobDescription = "We need a C# backend engineer with React experience.";

        var firstResponse = await _client.PostAsJsonAsync($"/api/matching/applications/{applicationId}",
            new ComputeJobMatchRequest(jobDescription), JsonOptions);
        firstResponse.EnsureSuccessStatusCode();
        var firstMatch = await firstResponse.Content.ReadFromJsonAsync<JobMatchResponse>(JsonOptions);
        firstMatch!.Score.ShouldBe(80);
        firstMatch.Recommendation.ShouldBe(JobMatchRecommendation.Apply);
        _fakeProvider.CallCount.ShouldBe(1);

        var secondResponse = await _client.PostAsJsonAsync($"/api/matching/applications/{applicationId}",
            new ComputeJobMatchRequest(jobDescription), JsonOptions);
        secondResponse.EnsureSuccessStatusCode();
        _fakeProvider.CallCount.ShouldBe(1, "unchanged CV + job description should be served from cache, not re-sent to the provider");

        var getResponse = await _client.GetAsync($"/api/matching/applications/{applicationId}");
        getResponse.EnsureSuccessStatusCode();
        var cached = await getResponse.Content.ReadFromJsonAsync<JobMatchResponse>(JsonOptions);
        cached!.ApplicationId.ShouldBe(applicationId);
    }

    [Fact]
    public async Task ComputeMatch_Recomputes_When_JobDescription_Changes()
    {
        await _client.PutAsJsonAsync("/api/matching/profile",
            new UpdateCandidateProfileRequest("C# / .NET / PostgreSQL"), JsonOptions);
        var applicationId = await CreateApplicationAsync();

        await _client.PostAsJsonAsync($"/api/matching/applications/{applicationId}",
            new ComputeJobMatchRequest("First job description."), JsonOptions);
        _fakeProvider.CallCount.ShouldBe(1);

        _fakeProvider.Result = new JobMatchProviderResult(40, ["Java"], ["C#", ".NET"], JobMatchRecommendation.Skip);

        var response = await _client.PostAsJsonAsync($"/api/matching/applications/{applicationId}",
            new ComputeJobMatchRequest("A completely different job description."), JsonOptions);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<JobMatchResponse>(JsonOptions);

        _fakeProvider.CallCount.ShouldBe(2);
        updated!.Score.ShouldBe(40);
        updated.Recommendation.ShouldBe(JobMatchRecommendation.Skip);
    }

    [Fact]
    public async Task GetMatch_Without_A_Prior_Compute_Returns_NotFound()
    {
        var applicationId = await CreateApplicationAsync();

        var response = await _client.GetAsync($"/api/matching/applications/{applicationId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
