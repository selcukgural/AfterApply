using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Analytics.Contracts;
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

namespace AfterApply.IntegrationTests.Analytics;

public class AnalyticsOverviewTests : IAsyncLifetime
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
            new RegisterRequest("analytics.test@example.com", "P@ssw0rd123!", "Analytics", "Test"), JsonOptions);
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

    private async Task<Guid> CreateApplicationAsync(string companyName, DateTimeOffset appliedAt)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status, DateTimeOffset changedAt)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, changedAt), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetOverview_Computes_Rates_ResponseTime_And_Distribution()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-30);

        // App1: Applied -> Rejected after 4 days. Responded, response time 4.0.
        var app1 = await CreateApplicationAsync("Rejected Co", appliedAt);
        await ChangeStatusAsync(app1, ApplicationStatus.Rejected, appliedAt.AddDays(4));

        // App2: Applied -> Screening (2 days, first response) -> Offer -> Accepted.
        // Never reaches an Interview status. Responded, response time 2.0.
        var app2 = await CreateApplicationAsync("Accepted Co", appliedAt);
        await ChangeStatusAsync(app2, ApplicationStatus.Screening, appliedAt.AddDays(2));
        await ChangeStatusAsync(app2, ApplicationStatus.Offer, appliedAt.AddDays(5));
        await ChangeStatusAsync(app2, ApplicationStatus.Accepted, appliedAt.AddDays(7));

        // App3: Applied -> Interview (6 days, first response) -> Ghosted.
        // Responded via the Interview transition even though current status is Ghosted.
        var app3 = await CreateApplicationAsync("Ghosted Co", appliedAt);
        await ChangeStatusAsync(app3, ApplicationStatus.Interview, appliedAt.AddDays(6));
        await ChangeStatusAsync(app3, ApplicationStatus.Ghosted, appliedAt.AddDays(20));

        // App4: Applied -> Withdrawn (candidate-initiated, not a response).
        var app4 = await CreateApplicationAsync("Withdrawn Co", appliedAt);
        await ChangeStatusAsync(app4, ApplicationStatus.Withdrawn, appliedAt.AddDays(1));

        // App5: stays at Applied, no response at all.
        await CreateApplicationAsync("Silent Co", appliedAt);

        var response = await _client.GetAsync("/api/analytics/overview");
        response.EnsureSuccessStatusCode();
        var overview = await response.Content.ReadFromJsonAsync<AnalyticsOverviewResponse>(JsonOptions);

        overview.ShouldNotBeNull();

        overview!.Rates.TotalApplications.ShouldBe(5);
        overview.Rates.RespondedCount.ShouldBe(3);
        overview.Rates.ResponseRate.ShouldBe(60.0);
        overview.Rates.InterviewCount.ShouldBe(1);
        overview.Rates.InterviewRate.ShouldBe(20.0);
        overview.Rates.OfferCount.ShouldBe(1);
        overview.Rates.OfferRate.ShouldBe(20.0);
        overview.Rates.RejectedCount.ShouldBe(1);
        overview.Rates.RejectionRate.ShouldBe(20.0);
        overview.Rates.GhostedCount.ShouldBe(1);
        overview.Rates.GhostingRate.ShouldBe(20.0);

        overview.ResponseTime.SampleSize.ShouldBe(3);
        overview.ResponseTime.AverageDays.ShouldBe(4.0);
        overview.ResponseTime.MedianDays.ShouldBe(4.0);

        overview.StatusDistribution.Sum(x => x.Count).ShouldBe(5);
        overview.StatusDistribution.Single(x => x.Status == ApplicationStatus.Rejected).Count.ShouldBe(1);
        overview.StatusDistribution.Single(x => x.Status == ApplicationStatus.Accepted).Count.ShouldBe(1);
        overview.StatusDistribution.Single(x => x.Status == ApplicationStatus.Ghosted).Count.ShouldBe(1);
        overview.StatusDistribution.Single(x => x.Status == ApplicationStatus.Withdrawn).Count.ShouldBe(1);
        overview.StatusDistribution.Single(x => x.Status == ApplicationStatus.Applied).Count.ShouldBe(1);
    }
}
