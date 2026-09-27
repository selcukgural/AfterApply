using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

public class ApplicationListPaginationTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, _factory.Services,
            new RegisterRequest("pagination.test@example.com", "P@ssw0rd123!", "Page", "Test", true));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

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

    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/4012345678/?refId=abc&trackingId=xyz")]
    [InlineData("https://www.linkedin.com/jobs/search/?currentJobId=4012345678&keywords=dotnet")]
    public async Task A_Pasted_Posting_Link_Finds_The_Application_Despite_Tracking_Parameters(string pasted)
    {
        await CreateApplicationWithUrlAsync("Linked Co", "https://www.linkedin.com/jobs/view/4012345678/");
        await CreateApplicationWithUrlAsync("Other Linked Co", "https://www.linkedin.com/jobs/view/4099999999/");

        var page = await SearchAsync(_client, pasted);

        page.TotalCount.ShouldBe(1);
        page.Items.Single().CompanyName.ShouldBe("Linked Co");
    }

    [Fact]
    public async Task A_Pasted_Posting_Link_Never_Finds_Another_Users_Application()
    {
        const string url = "https://www.kariyer.net/is-ilani/acme-yazilim-123456";
        var other = _factory.CreateClient();
        var otherAuth = await TestAccounts.RegisterVerifiedAsync(other, _factory.Services,
            new RegisterRequest("pagination.other@example.com", "P@ssw0rd123!", "Other", "User", true));
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherAuth!.AccessToken);
        var created = await other.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Someone Elses Co", "Engineer", url, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        created.EnsureSuccessStatusCode();

        var mine = await SearchAsync(_client, url + "?utm_source=share");
        mine.TotalCount.ShouldBe(0);

        var theirs = await SearchAsync(other, url + "?utm_source=share");
        theirs.TotalCount.ShouldBe(1);
    }

    private async Task CreateApplicationWithUrlAsync(string companyName, string jobUrl)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", jobUrl, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<PagedResult<ApplicationSummaryResponse>> SearchAsync(HttpClient client, string search)
    {
        var response = await client.GetAsync($"/api/applications?search={Uri.EscapeDataString(search)}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions))!;
    }

    [Theory]
    [InlineData("_")]
    [InlineData("%")]
    public async Task A_Wildcard_Typed_Into_Search_Is_Matched_Literally(string wildcard)
    {
        await CreateApplicationAsync("Plain Co", "Backend Engineer");
        await CreateApplicationAsync("Under_Score 100% Co", "Engineer");

        var response = await _client.GetAsync($"/api/applications?search={Uri.EscapeDataString(wildcard)}");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);

        page!.Items.Single().CompanyName.ShouldBe("Under_Score 100% Co");
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
