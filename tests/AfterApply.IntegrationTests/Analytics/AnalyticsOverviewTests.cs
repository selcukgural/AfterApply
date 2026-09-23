using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

namespace AfterApply.IntegrationTests.Analytics;

[Collection(IntegrationTestCollection.Name)]
public class AnalyticsOverviewTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("analytics.test@example.com", "P@ssw0rd123!", "Analytics", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

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

    [Fact]
    public async Task GetOverview_Returns_A_Twelve_Week_Application_Trend_Ending_This_Week()
    {
        // Two this week, one six weeks back, one well outside the 12-week window.
        await CreateApplicationAsync("Trend A", DateTimeOffset.UtcNow);
        await CreateApplicationAsync("Trend B", DateTimeOffset.UtcNow);
        await CreateApplicationAsync("Trend C", DateTimeOffset.UtcNow.AddDays(-42));
        await CreateApplicationAsync("Trend Old", DateTimeOffset.UtcNow.AddDays(-400));

        var response = await _client.GetAsync("/api/analytics/overview");
        response.EnsureSuccessStatusCode();
        var overview = await response.Content.ReadFromJsonAsync<AnalyticsOverviewResponse>(JsonOptions);

        overview.ShouldNotBeNull();
        var trend = overview!.ApplicationsPerWeek.ToList();

        trend.Count.ShouldBe(12);
        trend.ShouldAllBe(x => x.WeekStart.DayOfWeek == DayOfWeek.Monday);

        // Consecutive weeks, no gaps, oldest first.
        trend.Zip(trend.Skip(1)).ShouldAllBe(pair => pair.Second.WeekStart == pair.First.WeekStart.AddDays(7));

        var thisWeek = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        trend[^1].WeekStart.ShouldBe(thisWeek.AddDays(-(((int)thisWeek.DayOfWeek + 6) % 7)));
        trend[^1].Count.ShouldBe(2);
        trend[^7].Count.ShouldBe(1);

        // The 400-day-old application is outside the window and must not be counted.
        trend.Sum(x => x.Count).ShouldBe(3);
    }

    [Fact]
    public async Task GetFlow_Sorts_Applications_Into_The_Card_Nodes_Within_The_Period()
    {
        var now = DateTimeOffset.UtcNow;

        // Unanswered: applied 45 days ago, never heard back (past the 30-day ghosting threshold).
        await CreateApplicationAsync("Silent Co", now.AddDays(-45));

        // Awaiting: applied 5 days ago, still inside the threshold.
        await CreateApplicationAsync("Fresh Co", now.AddDays(-5));

        // Rejected before any interview, first reply after 3 days.
        var rejected = await CreateApplicationAsync("No Thanks Co", now.AddDays(-40));
        await ChangeStatusAsync(rejected, ApplicationStatus.Rejected, now.AddDays(-37));

        // Interviewed, then went silent. First reply after 9 days.
        var silent = await CreateApplicationAsync("Went Quiet Co", now.AddDays(-60));
        await ChangeStatusAsync(silent, ApplicationStatus.Interview, now.AddDays(-51));
        await ChangeStatusAsync(silent, ApplicationStatus.Ghosted, now.AddDays(-20));

        // Interviewed, then offered. First reply after 1 day.
        var offer = await CreateApplicationAsync("Yes Co", now.AddDays(-20));
        await ChangeStatusAsync(offer, ApplicationStatus.Screening, now.AddDays(-19));
        await ChangeStatusAsync(offer, ApplicationStatus.TechnicalInterview, now.AddDays(-15));
        await ChangeStatusAsync(offer, ApplicationStatus.Offer, now.AddDays(-5));

        // Outside the 90-day window: must not be counted for period=90.
        var old = await CreateApplicationAsync("Last Year Co", now.AddDays(-200));
        await ChangeStatusAsync(old, ApplicationStatus.Rejected, now.AddDays(-190));

        var flow = await GetFlowAsync("90");

        flow.Counts.ShouldBe(new ApplicationFlowCounts(
            Total: 5, Unanswered: 1, AwaitingReply: 1, RejectedBeforeInterview: 1, InScreening: 0,
            WithdrawnBeforeInterview: 0, Interviewed: 2, Offer: 1, InterviewInProgress: 0,
            RejectedAfterInterview: 0, SilentAfterInterview: 1, WithdrawnAfterInterview: 0));
        flow.MedianFirstReplyDays.ShouldBe(3.0);
        flow.FirstAppliedOn.ShouldBe(DateOnly.FromDateTime(now.AddDays(-60).UtcDateTime));
        flow.Today.ShouldBe(DateOnly.FromDateTime(now.UtcDateTime));

        var all = await GetFlowAsync("all");
        all.Counts.Total.ShouldBe(6);
        all.Counts.RejectedBeforeInterview.ShouldBe(2);

        var last30 = await GetFlowAsync("30");
        last30.Counts.Total.ShouldBe(2);
        last30.Counts.AwaitingReply.ShouldBe(1);
        last30.Counts.Offer.ShouldBe(1);
    }

    [Fact]
    public async Task GetFlow_Counts_Only_The_Callers_Own_Applications()
    {
        await CreateApplicationAsync("Mine Co", DateTimeOffset.UtcNow.AddDays(-2));

        var other = _factory.CreateClient();
        var registerResponse = await other.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("analytics.flow.other@example.com", "P@ssw0rd123!", "Other", "User", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await other.GetAsync("/api/analytics/flow?period=all");
        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApplicationFlowResponse>(JsonOptions);

        flow!.Counts.Total.ShouldBe(0);
        flow.FirstAppliedOn.ShouldBeNull();
        flow.MedianFirstReplyDays.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?period=60")]
    [InlineData("?period=week")]
    public async Task GetFlow_Refuses_An_Unknown_Period(string query)
    {
        var response = await _client.GetAsync($"/api/analytics/flow{query}");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFlow_Requires_Authentication()
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync("/api/analytics/flow?period=90");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    private async Task<ApplicationFlowResponse> GetFlowAsync(string period)
    {
        var response = await _client.GetAsync($"/api/analytics/flow?period={period}");
        response.EnsureSuccessStatusCode();
        var flow = await response.Content.ReadFromJsonAsync<ApplicationFlowResponse>(JsonOptions);
        flow.ShouldNotBeNull();
        return flow!;
    }
}
