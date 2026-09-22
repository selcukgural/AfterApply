using System.Net.Http.Json;
using AfterApply.Application.SiteStats;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.SiteStats;

/// <summary>A low floor and no caching, so one test can seed a handful of rows and read the
/// figures straight back; the production floor (25) is asserted separately as configuration.</summary>
public sealed class SiteStatsProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("SiteStats:MinimumCount", "3");
        builder.UseSetting("SiteStats:CacheSeconds", "1");
    }
}

[Collection(IntegrationTestCollection.Name)]
public class SiteStatsTests(ApiHost<SiteStatsProfile> host) : IClassFixture<ApiHost<SiteStatsProfile>>, IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Figures_Under_The_Floor_Are_Null_And_Those_Over_It_Are_Counts()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            // Three scans reach the floor; two benchmark answers do not; no reviews at all.
            for (var i = 0; i < 3; i++)
            {
                db.CvScanResults.Add(CvScanResult.Create(70 + i, CvFileFormat.Pdf, false, now));
            }
            for (var i = 0; i < 2; i++)
            {
                db.BenchmarkSubmissions.Add(BenchmarkSubmission.Create(30, 3, BenchmarkSector.SoftwareAndIt,
                    BenchmarkPeriod.LastThreeMonths, null, null, "tr", null, now));
            }
            await db.SaveChangesAsync();
        }

        // The entry may have been cached empty by another test's request; wait it out.
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/site-stats");
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl!.Public.ShouldBeTrue();

        var stats = await response.Content.ReadFromJsonAsync<SiteStatsResponse>(ApiHost.JsonOptions);
        stats.ShouldNotBeNull();
        stats!.CvScans.ShouldBe(3);
        stats.BenchmarkAnswers.ShouldBeNull();
        stats.PublishedReviews.ShouldBeNull();
    }

    [Fact]
    public async Task The_Endpoint_Is_Anonymous_And_Leaves_No_Audit_Row()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/site-stats")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // A GET takes no input; the request-audit middleware only records writes.
        db.RequestAudits.Count().ShouldBe(0);
    }
}
