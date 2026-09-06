using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.IntegrationTests.Caching;

/// <summary>
/// Covers the Redis removal (DECISIONS.md 2026-09-06). The host below is configured exactly like
/// production minus the Memorystore instance — no ConnectionStrings:Redis at all — so "the API
/// still boots and serves traffic without Redis" is not asserted separately: every test in this
/// class, and every other class in the suite, only passes because it does.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CacheConfigurationTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CacheConfigurationTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("cache.config@example.com", "P@ssw0rd123!", "Cache", "Config", true), JsonOptions);
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

    /// <summary>The invariant that actually matters: HybridCache promotes itself to a two-level
    /// cache the moment any IDistributedCache is in the container, so an absent registration — not
    /// an absent connection string — is what keeps it L1-only.</summary>
    [Fact]
    public void HybridCache_Runs_Without_A_Distributed_Cache_Backend()
    {
        _factory!.Services.GetService<HybridCache>().ShouldNotBeNull();
        _factory.Services.GetService<IDistributedCache>().ShouldBeNull();
    }

    /// <summary>L1-only makes bounding L1 the whole safety story. MemoryCache does no size-based
    /// eviction at all while SizeLimit is null, and the key space includes company-search:{query},
    /// whose cardinality comes from user input — so an unbounded L1 is a memory-exhaustion vector
    /// against a Cloud Run container, not just untidiness.</summary>
    [Fact]
    public void In_Memory_Cache_Is_Size_Bounded()
    {
        var memoryCacheOptions = _factory!.Services.GetRequiredService<IOptions<MemoryCacheOptions>>().Value;
        memoryCacheOptions.SizeLimit.ShouldBe(16 * 1024 * 1024);

        var hybridCacheOptions = _factory.Services.GetRequiredService<IOptions<HybridCacheOptions>>().Value;
        hybridCacheOptions.MaximumPayloadBytes.ShouldBe(1024 * 1024);
        hybridCacheOptions.MaximumKeyLength.ShouldBe(512);
    }

    [Fact]
    public async Task Health_Check_Reports_Postgres_And_Nothing_Else()
    {
        var registrations = _factory!.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Select(registration => registration.Name).ToArray();

        registrations.ShouldBe(["postgres"]);

        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    /// <summary>The behaviour the L2 was assumed to protect. Summary counts are cached for 20s and
    /// evicted by ChangeStatusAsync; with a single host, eviction reaching L1 is the whole of
    /// correctness, which is exactly what it was with Redis too — the L2 was never consulted while
    /// an unexpired L1 entry existed, because LocalCacheExpiration equals Expiration.</summary>
    [Fact]
    public async Task Summary_Counts_Are_Refreshed_Immediately_After_A_Status_Change()
    {
        var applicationId = await CreateApplicationAsync("Cache Co", "Engineer");

        // Populates the cache entry that the status change below has to evict. Without this read
        // first, the test would pass even if invalidation were broken.
        var beforeChange = await GetSummaryAsync();
        beforeChange.Total.ShouldBe(1);
        beforeChange.Rejected.ShouldBe(0);

        await ChangeStatusAsync(applicationId, ApplicationStatus.Rejected);

        var afterChange = await GetSummaryAsync();
        afterChange.Total.ShouldBe(1);
        afterChange.Rejected.ShouldBe(1);
    }

    /// <summary>CompanyResolver caches a "not found" null for 10 minutes before inserting, then
    /// overwrites it with the new id. That overwrite is the only thing stopping the second
    /// application from reading the stale null and inserting a duplicate Company, which the unique
    /// index on NormalizedName would reject — so it is worth pinning now that the write lands in
    /// L1 alone.</summary>
    [Fact]
    public async Task Resolving_The_Same_Company_Twice_Reuses_The_First_Company()
    {
        var first = await CreateApplicationAsync("Repeated Cache Co", "Engineer");
        var second = await CreateApplicationAsync("Repeated Cache Co", "Senior Engineer");

        var firstDetail = await GetApplicationAsync(first);
        var secondDetail = await GetApplicationAsync(second);

        secondDetail.CompanyId.ShouldBe(firstDetail.CompanyId);
    }

    private async Task<ApplicationSummaryCountsResponse> GetSummaryAsync()
    {
        var response = await _client.GetAsync("/api/applications/summary");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationSummaryCountsResponse>(JsonOptions))!;
    }

    private async Task<ApplicationDetailResponse> GetApplicationAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    private async Task<Guid> CreateApplicationAsync(string companyName, string jobTitle)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, jobTitle, null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
    }
}
