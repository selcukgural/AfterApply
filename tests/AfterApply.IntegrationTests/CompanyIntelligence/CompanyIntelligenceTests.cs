using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests.CompanyIntelligence;

public class CompanyIntelligenceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    // Flag left at its real appsettings.json default (false) — used both to assert every
    // endpoint 404s while the flag is off, and (via direct DI, bypassing HTTP) to prove the
    // aggregation pipeline itself is correct even while disabled in prod. HiddenBelow is
    // overridden to 2 purely so a handful of seeded applications is enough to exercise
    // non-Hidden confidence buckets — it does not affect the Enabled flag itself.
    private WebApplicationFactory<Program>? _defaultFactory;

    // Same connection string / thresholds, only Enabled flipped to true.
    private WebApplicationFactory<Program>? _enabledFactory;

    private HttpClient _client = null!;
    private HttpClient _clientB = null!;
    private HttpClient _enabledClient = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _defaultFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("CompanyIntelligence:HiddenBelow", "2");
        });

        using (var scope = _defaultFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        _enabledFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("CompanyIntelligence:HiddenBelow", "2");
            builder.UseSetting("CompanyIntelligence:Enabled", "true");
        });

        _client = await CreateAuthenticatedClientAsync(_defaultFactory, "ci.default.a@example.com");
        _clientB = await CreateAuthenticatedClientAsync(_defaultFactory, "ci.default.b@example.com");
        _enabledClient = await CreateAuthenticatedClientAsync(_enabledFactory, "ci.enabled@example.com");
    }

    public async Task DisposeAsync()
    {
        if (_defaultFactory is not null)
        {
            await _defaultFactory.DisposeAsync();
        }

        if (_enabledFactory is not null)
        {
            await _enabledFactory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "CI", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<(Guid ApplicationId, Guid CompanyId)> CreateApplicationAsync(
        HttpClient client, string companyName, DateTimeOffset appliedAt)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return (created!.Id, created.CompanyId);
    }

    private static async Task ChangeStatusAsync(
        HttpClient client, Guid applicationId, ApplicationStatus status, DateTimeOffset changedAt)
    {
        var response = await client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, changedAt), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Endpoint_Returns_NotFound_For_Existing_Company_When_Flag_Disabled()
    {
        var (_, companyId) = await CreateApplicationAsync(_client, "Flag Off Co", DateTimeOffset.UtcNow.AddDays(-5));

        var response = await _client.GetAsync($"/api/company-intelligence/{companyId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoint_Returns_NotFound_For_Unknown_Company_When_Flag_Disabled()
    {
        var response = await _client.GetAsync($"/api/company-intelligence/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Aggregation_Computes_Correctly_Across_Multiple_Users_Even_While_Flag_Disabled()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-30);

        // Two different registered users applying to the same company — CompanyResolver's
        // find-or-create-by-NormalizedName is what's expected to land both on one CompanyId.
        var (app1, company1) = await CreateApplicationAsync(_client, "Cross User Co", appliedAt);
        await ChangeStatusAsync(_client, app1, ApplicationStatus.Interview, appliedAt.AddDays(3));

        var (app2, company2) = await CreateApplicationAsync(_clientB, "Cross User Co", appliedAt);
        await ChangeStatusAsync(_clientB, app2, ApplicationStatus.Rejected, appliedAt.AddDays(5));

        company1.ShouldBe(company2);

        using var scope = _defaultFactory!.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        var result = await service.GetByCompanyIdAsync(company1, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.VeryLow); // 2 apps: >= HiddenBelow(2), < VeryLowBelow(50)
        result.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2);
        result.Metrics.ResponseRate.ShouldBe(100.0);
        result.Metrics.InterviewRate.ShouldBe(50.0);
        result.Metrics.OfferRate.ShouldBe(0.0);
        result.Metrics.GhostingRate.ShouldBe(0.0);
        result.Metrics.AverageResponseTimeDays.ShouldBe(4.0);
        result.Metrics.MedianResponseTimeDays.ShouldBe(4.0);
    }

    [Fact]
    public async Task GetByCompanyId_Returns_Hidden_With_Null_Metrics_Below_Threshold()
    {
        var (_, companyId) = await CreateApplicationAsync(_client, "Single App Co", DateTimeOffset.UtcNow.AddDays(-2));

        using var scope = _defaultFactory!.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        var result = await service.GetByCompanyIdAsync(companyId, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden); // 1 app < HiddenBelow(2)
        result.Metrics.ShouldBeNull();
    }

    [Fact]
    public async Task Endpoint_Returns_Ok_With_Metrics_When_Flag_Enabled_And_Above_Threshold()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var (_, companyId) = await CreateApplicationAsync(_enabledClient, "Enabled Co", appliedAt);
        await CreateApplicationAsync(_enabledClient, "Enabled Co", appliedAt);

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result!.CompanyId.ShouldBe(companyId);
        result.Confidence.ShouldNotBe(ConfidenceBucket.Hidden);
        result.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2);
    }

    [Fact]
    public async Task Endpoint_Returns_Hidden_With_Null_Metrics_When_Flag_Enabled_And_Below_Threshold()
    {
        var (_, companyId) = await CreateApplicationAsync(_enabledClient, "Hidden Enabled Co", DateTimeOffset.UtcNow.AddDays(-1));

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden);
        result.Metrics.ShouldBeNull();
    }

    [Fact]
    public async Task Endpoint_Returns_NotFound_For_Unknown_Company_When_Flag_Enabled()
    {
        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
