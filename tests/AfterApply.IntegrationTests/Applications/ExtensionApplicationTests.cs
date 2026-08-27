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

namespace AfterApply.IntegrationTests.Applications;

// Covers POST /api/applications/from-extension (Sprint 9's "I Applied" action): Job/Company
// resolution via the existing resolvers, the FullTime EmploymentType default (same known
// limitation as generic CSV import, DECISIONS.md Sprint 4), and same-JobUrl dedup returning the
// existing row instead of creating a duplicate when the button is effectively clicked twice.
public class ExtensionApplicationTests : IAsyncLifetime
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
            new RegisterRequest("extension.test@example.com", "P@ssw0rd123!", "Extension", "Test", true), JsonOptions);
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
    }

    [Fact]
    public async Task Create_Resolves_Company_And_Sets_BrowserExtension_Source_And_FullTime_Default()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Corp", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/4449445627/", "Istanbul", "We build things.", DateTimeOffset.UtcNow.AddDays(-1)),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        result!.WasDuplicate.ShouldBeFalse();
        result.Application.CompanyName.ShouldBe("Acme Corp");
        result.Application.JobTitle.ShouldBe("Backend Engineer");
        result.Application.Source.ShouldBe(Source.BrowserExtension);
        result.Application.EmploymentType.ShouldBe(EmploymentType.FullTime);
    }

    [Fact]
    public async Task Create_With_Same_JobUrl_Twice_Returns_Existing_Application_As_Duplicate()
    {
        var request = new CreateFromExtensionRequest("Acme Corp", "Backend Engineer",
            "https://www.linkedin.com/jobs/view/4449445627/", "Istanbul", null, null);

        var firstResponse = await _client.PostAsJsonAsync("/api/applications/from-extension", request, JsonOptions);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        first!.WasDuplicate.ShouldBeFalse();

        var secondResponse = await _client.PostAsJsonAsync("/api/applications/from-extension", request, JsonOptions);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        second!.WasDuplicate.ShouldBeTrue();
        second.Application.Id.ShouldBe(first.Application.Id);

        var listResponse = await _client.GetAsync("/api/applications");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);
        list!.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Create_With_High_Confidence_Fuzzy_Match_Attaches_To_Existing_Company()
    {
        // Seeded manually (not via extension) so the two applications don't collide on JobUrl
        // dedup — this test is specifically about company resolution, not the JobUrl dedup path
        // already covered above.
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Nova Yazilim", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        // A single-character typo of the seeded name — a near-duplicate, not a genuinely new
        // company — clears the default 0.75 trigram similarity threshold.
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Nova Yazlim", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/1111111111/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldBe(seeded!.CompanyId);
    }

    [Fact]
    public async Task Create_With_Low_Confidence_Match_Creates_New_Company()
    {
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Zeta Robotics", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Delta Analytics", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/2222222222/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldNotBe(seeded!.CompanyId);
    }
}
