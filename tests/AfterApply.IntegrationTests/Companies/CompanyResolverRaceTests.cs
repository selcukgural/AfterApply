using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Companies;

/// <summary>
/// CompanyResolver caches "no such company" for ten minutes. The backplane closes the window in
/// which another instance can create the company behind that cached miss to milliseconds, and
/// while Redis is unreachable the window is the whole TTL; either way the NormalizedName index
/// then refuses the resolver's insert, and the right answer is the row that won — not a 500.
/// The stale miss is planted directly in the cache here, and the winning row is inserted
/// straight into the table, past the cache, to reproduce the window deterministically.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyResolverRaceTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Company_Created_Behind_A_Cached_Miss_Is_Returned_Not_Duplicated()
    {
        var (client, _) = await host.RegisterAsync("resolver.race@example.com");
        const string name = "Race Condition Co";
        var normalized = CompanyNameNormalizer.Normalize(name);

        var cache = host.Services.GetRequiredService<HybridCache>();
        await cache.SetAsync<Guid?>($"company:normalized:{normalized}", null);

        var winner = await host.WithDbAsync(async db =>
        {
            var company = Company.Create(name, DateTimeOffset.UtcNow, slug: "race-condition-co");
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            return company.Id;
        });

        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            name, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        created!.CompanyId.ShouldBe(winner);
        (await host.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(db.Companies, c => c.NormalizedName == normalized))).ShouldBe(1);
    }
}
