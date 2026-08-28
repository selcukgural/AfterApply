using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Applications;

public class ApplicationListPaginationTests : IAsyncLifetime
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
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("pagination.test@example.com", "P@ssw0rd123!", "Page", "Test", true), JsonOptions);
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

    private async Task<Guid> CreateApplicationAsync(string companyName, string jobTitle)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, jobTitle, null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
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

    [Fact]
    public async Task GetAll_Paginates_And_Reports_TotalCount()
    {
        for (var i = 1; i <= 5; i++)
        {
            await CreateApplicationAsync($"Page Co {i}", $"Engineer {i}");
        }

        var response = await _client.GetAsync("/api/applications?page=2&pageSize=2");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);

        page.ShouldNotBeNull();
        page!.TotalCount.ShouldBe(5);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAll_Filters_By_Search_Term()
    {
        await CreateApplicationAsync("Searchable Robotics", "Firmware Engineer");
        await CreateApplicationAsync("Other Co", "Backend Engineer");

        var response = await _client.GetAsync("/api/applications?search=Searchable");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);

        page!.TotalCount.ShouldBe(1);
        page.Items.Single().CompanyName.ShouldBe("Searchable Robotics");
    }

    [Fact]
    public async Task GetAll_Filters_By_Status()
    {
        var interviewingId = await CreateApplicationAsync("Interviewing Co", "Engineer");
        await ChangeStatusAsync(interviewingId, ApplicationStatus.Interview);
        await CreateApplicationAsync("Still Applied Co", "Engineer");

        var response = await _client.GetAsync("/api/applications?status=Interview");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);

        page!.TotalCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(interviewingId);
    }

    [Fact]
    public async Task Summary_Computes_Buckets_From_Actual_Statuses()
    {
        var applied = await CreateApplicationAsync("Applied Co", "Engineer");
        var interviewing = await CreateApplicationAsync("Interviewing Co", "Engineer");
        await ChangeStatusAsync(interviewing, ApplicationStatus.Screening);
        await ChangeStatusAsync(interviewing, ApplicationStatus.Interview);
        var offered = await CreateApplicationAsync("Offer Co", "Engineer");
        await ChangeStatusAsync(offered, ApplicationStatus.Offer);
        var rejected = await CreateApplicationAsync("Rejected Co", "Engineer");
        await ChangeStatusAsync(rejected, ApplicationStatus.Rejected);
        var ghosted = await CreateApplicationAsync("Ghosted Co", "Engineer");
        await ChangeStatusAsync(ghosted, ApplicationStatus.Ghosted);
        _ = applied;

        var response = await _client.GetAsync("/api/applications/summary");
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ApplicationSummaryCountsResponse>(JsonOptions);

        summary.ShouldNotBeNull();
        summary!.Total.ShouldBe(5);
        summary.Active.ShouldBe(2); // Applied + Interview
        summary.Interviews.ShouldBe(1);
        summary.Waiting.ShouldBe(1);
        summary.Offers.ShouldBe(1);
        summary.Rejected.ShouldBe(1);
        summary.Ghosted.ShouldBe(1);
    }
}
