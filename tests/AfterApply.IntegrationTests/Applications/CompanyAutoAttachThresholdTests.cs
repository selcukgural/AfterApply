using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Applications;

// Proves Companies:FuzzyMatchThreshold is actually read from configuration, not hard-coded —
// same pair-classification as CompanyIntelligenceOptions' own "not hard-coded" test convention
// (DECISIONS.md Sprint 10). Own WebApplicationFactory instance (rather than reusing
// ExtensionApplicationTests') specifically so it can override this one setting without affecting
// the other tests' default-threshold behavior.
public class CompanyAutoAttachThresholdTests : IAsyncLifetime
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
            // A near-1.0 threshold means even a one-character typo (the same fixture used by
            // ExtensionApplicationTests' default-threshold test) no longer clears it.
            builder.UseSetting("Companies:FuzzyMatchThreshold", "0.99");
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("threshold.test@example.com", "P@ssw0rd123!", "Threshold", "Test", true), JsonOptions);
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
    public async Task Raised_Threshold_Prevents_Auto_Attach_For_A_Match_That_Would_Otherwise_Qualify()
    {
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Nova Yazilim", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Nova Yazlim", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/3333333333/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldNotBe(seeded!.CompanyId);
    }
}
