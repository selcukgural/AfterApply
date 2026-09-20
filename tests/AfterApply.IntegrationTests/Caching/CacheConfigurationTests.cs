using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Api.Imports;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace AfterApply.IntegrationTests.Caching;

/// <summary>
/// Covers the cache's wiring (DECISIONS.md 2026-09-18 "Redis geri geldi"): FusionCache behind the
/// HybridCache abstraction, the DI MemoryCache as L1, Redis as L2 and as the backplane, and the
/// health check that reports Redis as Degraded rather than Unhealthy. The cross-instance
/// behaviour — the reason for all of it — is in <see cref="CrossInstanceInvalidationTests" />.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CacheConfigurationTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("cache.config@example.com", "P@ssw0rd123!", "Cache", "Config", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The services depend on the HybridCache abstract class; what they get must be
    /// FusionCache's adapter over the one IFusionCache, with a Redis L2 and a backplane behind
    /// it. Microsoft's DefaultHybridCache would satisfy the first assertion and none of the
    /// others — and it has no backplane, which is the whole point.</summary>
    [Fact]
    public void HybridCache_Is_FusionCache_Over_Redis_With_A_Backplane()
    {
        var hybridCache = _factory!.Services.GetRequiredService<HybridCache>();
        hybridCache.GetType().Name.ShouldBe("FusionHybridCache");

        var fusionCache = _factory.Services.GetRequiredService<IFusionCache>();
        fusionCache.HasDistributedCache.ShouldBeTrue();
        fusionCache.HasBackplane.ShouldBeTrue();

        // The Redis IDistributedCache is FusionCache's own, not a container-wide registration:
        // nothing else in the app should reach for a distributed cache directly.
        _factory.Services.GetService<IDistributedCache>().ShouldBeNull();
        _factory.Services.GetService<IConnectionMultiplexer>().ShouldNotBeNull();
    }

    /// <summary>The L1 is the DI MemoryCache with a SizeLimit: the key space includes
    /// company-search:{query}, whose cardinality comes from user input, so an unbounded L1 is a
    /// memory-exhaustion vector against a Cloud Run container. FusionCache does not stamp entries
    /// with their byte size the way DefaultHybridCache did, so the unit is entries and every
    /// entry must weigh 1 — a size-limited MemoryCache throws on an entry with no Size.</summary>
    [Fact]
    public void In_Memory_Cache_Is_Size_Bounded()
    {
        var memoryCacheOptions = _factory!.Services.GetRequiredService<IOptions<MemoryCacheOptions>>().Value;
        memoryCacheOptions.SizeLimit.ShouldBe(10_000);

        var fusionCacheOptions = _factory.Services.GetRequiredService<IOptions<FusionCacheOptions>>().Value;
        fusionCacheOptions.DefaultEntryOptions.Size.ShouldBe(1);
        fusionCacheOptions.CacheKeyPrefix.ShouldNotBeNullOrEmpty();
        fusionCacheOptions.BackplaneChannelPrefix.ShouldBe(host.Stores.ChannelPrefix);
    }

    [Fact]
    public async Task Health_Check_Reports_Postgres_And_Redis()
    {
        var registrations = _factory!.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.ToArray();

        registrations.Select(registration => registration.Name).ShouldBe(["postgres", "redis"]);
        // Redis being down must not take the API down with it: the cache degrades to L1, so the
        // check degrades too. Postgres keeps its default (Unhealthy → 503).
        registrations.Single(registration => registration.Name == "redis").FailureStatus.ShouldBe(HealthStatus.Degraded);

        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    /// <summary>
    /// Shutting a host down must not throw whatever order its Redis users were first touched in.
    /// Everything shares one multiplexer, and FusionCache's backplane disposes that connection
    /// when it is disposed, factory-provided or not. The container disposes singletons in reverse
    /// order of creation, so when the SignalR hub manager was older than the cache, the cache's
    /// disposal closed the connection under the manager's own Dispose (UnsubscribeAll →
    /// ObjectDisposedException out of Host.Dispose): "Test Class Cleanup Failure" on one CI run in
    /// three, 2026-09-19/20, the trace in the run's trx. Program.cs now creates the connection and
    /// the cache before the host starts, so they are the oldest objects and go last. This test
    /// forces the order that failed: the hub manager connected before any cache use.
    /// </summary>
    [Fact]
    public async Task Disposing_A_Host_Whose_Hub_Manager_Was_Created_Before_The_Cache_Does_Not_Throw()
    {
        var standalone = host.Standalone(_ => { });

        // The SignalR Redis hub manager first, and connected: a send is what makes it subscribe.
        await standalone.Services.GetRequiredService<IHubContext<ImportProgressHub>>().Clients.All.SendAsync("noop", "x");

        // Then the cache: a request that reads through L2.
        var (client, _) = await host.RegisterAsync("dispose.order@example.com", on: standalone);
        (await client.GetFromJsonAsync<ApplicationSummaryCountsResponse>("/api/applications/summary", JsonOptions))!.Total.ShouldBe(0);

        await Should.NotThrowAsync(async () => await standalone.DisposeAsync());
    }

    /// <summary>Redis being unreachable must cost latency at most, never availability: the cache
    /// degrades to L1 (FusionCache swallows the L2 and backplane failures — ReThrow*Exceptions are
    /// off, the circuit breaker stops retrying), requests keep answering, and /health says
    /// Degraded with a 200 rather than Unhealthy with a 503. Port 1 answers nothing on any machine;
    /// abortConnect=false is what production's secret carries too, so the host boots the same way.</summary>
    [Fact]
    public async Task Without_Redis_The_Api_Serves_From_L1_And_Health_Is_Degraded()
    {
        await using var withoutRedis = host.Standalone(builder =>
        {
            builder.UseSetting("ConnectionStrings:Redis", "localhost:1,abortConnect=false,connectTimeout=300,syncTimeout=300");
            builder.UseSetting("Redis:WaitForBackplaneSubscribe", "false");
        });
        var (client, _) = await host.RegisterAsync("no.redis@example.com", on: withoutRedis);

        var created = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "No Redis Co", "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Read twice: the first populates L1 (and fails to write L2), the second is served from it.
        (await client.GetFromJsonAsync<ApplicationSummaryCountsResponse>("/api/applications/summary", JsonOptions))!.Total.ShouldBe(1);
        (await client.GetFromJsonAsync<ApplicationSummaryCountsResponse>("/api/applications/summary", JsonOptions))!.Total.ShouldBe(1);

        var health = await withoutRedis.CreateClient().GetAsync("/health");
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).ShouldBe("Degraded");
    }

    /// <summary>Summary counts are cached for 20s and evicted by ChangeStatusAsync; on a single
    /// host, eviction reaching L1 is the whole of correctness. The multi-host version of the same
    /// assertion is in CrossInstanceInvalidationTests.</summary>
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
    /// index on NormalizedName would reject — and a cached null now has to survive the trip
    /// through Redis and back as "null was cached", not "nothing was cached".</summary>
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
