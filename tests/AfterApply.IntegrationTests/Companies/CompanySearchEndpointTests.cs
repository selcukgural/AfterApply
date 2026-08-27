using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests.Companies;

// Covers GET /api/companies/search — the pg_trgm-backed autocomplete used by the web "add
// application" form and the browser extension popup. Companies are seeded the same way the rest
// of the suite does it: through the public API (manual application create), not by writing
// directly to the DbContext, since Company is shared/global reference data with no dedicated
// create endpoint of its own.
public class CompanySearchEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("company.search.test@example.com", "P@ssw0rd123!", "Search", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Seed via the manual create endpoint — Company has no dedicated create endpoint of its
        // own, it's always resolved-or-created as a side effect of creating an Application.
        foreach (var companyName in new[] { "Google", "Google Cloud", "Goldman Sachs" })
        {
            var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
                companyName, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null), JsonOptions);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Search_Returns_Ranked_Matches_For_Partial_Prefix()
    {
        var response = await _client.GetAsync("/api/companies/search?q=Goo");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<CompanySearchResultResponse>>(JsonOptions);

        // "Goldman Sachs" has no "goo" substring and is a weaker trigram match than either
        // Google entry, so it must not appear ahead of them (or at all, at this query length).
        var names = results!.Select(r => r.Name).ToList();
        names.ShouldContain("Google");
        names.ShouldContain("Google Cloud");
        names.ShouldNotContain("Goldman Sachs");
    }

    [Fact]
    public async Task Search_Below_MinQueryLength_Returns_Empty()
    {
        var response = await _client.GetAsync("/api/companies/search?q=G");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<CompanySearchResultResponse>>(JsonOptions);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_Typo_Still_Matches_Via_Trigram()
    {
        // No substring match ("gogle" is not contained in "google"'s normalized form), so this
        // only passes if the trigram similarity fallback (not just ILIKE) is actually wired up.
        var response = await _client.GetAsync("/api/companies/search?q=Gogle");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<CompanySearchResultResponse>>(JsonOptions);

        results!.Select(r => r.Name).ShouldContain("Google");
    }
}
