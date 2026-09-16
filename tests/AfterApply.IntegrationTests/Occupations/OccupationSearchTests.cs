using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.Occupations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Occupations;

public sealed class OccupationSearchProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
    }
}

/// <summary>The catalogue typeahead against the seeded rows: both languages match whatever was
/// typed, Turkish i-variants fold, prefixes rank first, short queries answer empty, retired rows
/// are gone, and reading needs an account.</summary>
[Collection(IntegrationTestCollection.Name)]
public class OccupationSearchTests(ApiHost<OccupationSearchProfile> host) : IClassFixture<ApiHost<OccupationSearchProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = _factory!.CreateClient();
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("occupation.search@example.com", "P@ssw0rd123!", "Occupation", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<IReadOnlyList<OccupationSearchResultResponse>> SearchAsync(string q) =>
        (await _client.GetFromJsonAsync<IReadOnlyList<OccupationSearchResultResponse>>($"/api/occupations/search?q={Uri.EscapeDataString(q)}", JsonOptions))!;

    [Fact]
    public async Task The_Seed_Is_There()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Occupations.CountAsync()).ShouldBeGreaterThanOrEqualTo(436 + 150);
        (await db.Occupations.SingleAsync(o => o.Code == "2512")).NameEn.ShouldBe("Software Developers");
    }

    [Fact]
    public async Task Reading_Requires_An_Account()
    {
        (await _factory!.CreateClient().GetAsync("/api/occupations/search?q=software")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_English_Query_Finds_A_Row_By_Its_English_Name_And_Returns_Both_Names()
    {
        var results = await SearchAsync("backend");

        var backend = results.Single(r => r.NameEn == "Backend Developer");
        backend.NameTr.ShouldBe("Backend Developer");
        backend.Code.ShouldStartWith("EK-");
    }

    [Fact]
    public async Task A_Turkish_Query_Folds_The_I_Variants()
    {
        var lower = await SearchAsync("yazılım geliştiricileri");
        var upper = await SearchAsync("YAZILIM GELİŞTİRİCİLERİ");
        var ascii = await SearchAsync("yazilim gelistiricileri");

        lower.ShouldContain(r => r.Code == "2512");
        upper.ShouldContain(r => r.Code == "2512");
        // ASCII-only typing still lands through the fuzzy net.
        ascii.ShouldContain(r => r.Code == "2512");
    }

    [Fact]
    public async Task A_Prefix_Match_Ranks_Above_A_Mid_Word_Match()
    {
        var results = await SearchAsync("data");

        results.Count.ShouldBeGreaterThan(1);
        results[0].NameEn.ShouldStartWith("Data", Case.Insensitive);
    }

    [Fact]
    public async Task Short_Queries_Answer_Empty_And_Results_Are_Capped()
    {
        (await SearchAsync("d")).ShouldBeEmpty();
        (await SearchAsync("")).ShouldBeEmpty();
        (await SearchAsync("er")).Count.ShouldBe(10);
    }

    [Fact]
    public async Task A_Retired_Row_Is_Not_Offered()
    {
        (await SearchAsync("legislators")).ShouldContain(r => r.Code == "1111");

        await using (var scope = _factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Occupations.SingleAsync(o => o.Code == "1111")).Retire();
            await db.SaveChangesAsync();
        }

        try
        {
            // A different query string: the previous answer is cached for a while by design.
            (await SearchAsync("legislator")).ShouldNotContain(r => r.Code == "1111");
        }
        finally
        {
            // The catalogue survives the per-test reset (it is seeded, not test data), so put the
            // row back for whichever test runs next.
            await using var scope = _factory!.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Occupations.Where(o => o.Code == "1111").ExecuteUpdateAsync(u => u.SetProperty(o => o.IsActive, true));
        }
    }

    [Fact]
    public async Task Ids_Match_The_Seed_Derivation()
    {
        var results = await SearchAsync("software developers");

        results.ShouldContain(r => r.Id == Occupation.IdFor("2512"));
    }
}
