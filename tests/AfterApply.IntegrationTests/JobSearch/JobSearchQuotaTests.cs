using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>
/// The two ceilings, counted from the ledger: a user's day and the product's month. The user
/// one is checked first so an account that has spent its day learns nothing about the shared
/// month; cache hits never count; the provider's own "0 remaining" ends the month early.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchQuotaTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string AdminEmail = "quota.admin@ekariyerim.com";

    private JobSearchTestHost _host = null!;
    private HttpClient _alice = null!;
    private HttpClient _bob = null!;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        _host = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchQuotaTests), new Dictionary<string, string?>
        {
            ["JobSearch:PerUserDailyCredits"] = "3",
            ["JobSearch:GlobalMonthlyCredits"] = "5"
        });
        _alice = await _host.RegisterAsync("quota.alice@example.com");
        _bob = await _host.RegisterAsync("quota.bob@example.com");
        _admin = await _host.RegisterAsync(AdminEmail);
        await _host.SetAdminAsync(AdminEmail);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task The_Fourth_Credit_Of_The_Day_Is_Refused_Before_It_Is_Sent()
    {
        for (var i = 1; i <= 3; i++)
        {
            (await _alice.GetAsync($"/api/job-search/jobs?query=day{i}")).EnsureSuccessStatusCode();
        }

        var response = await _alice.GetAsync("/api/job-search/jobs?query=day4");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("You have used today's 3 job search credits. Come back tomorrow.");
        _host.Handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task A_Request_That_Would_Cross_The_Ceiling_Is_Refused_Whole()
    {
        (await _alice.GetAsync("/api/job-search/jobs?query=two&numPages=2")).EnsureSuccessStatusCode();

        var response = await _alice.GetAsync("/api/job-search/jobs?query=more&numPages=2");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _host.Handler.CallCount.ShouldBe(1);
        var usage = await _alice.GetFromJsonAsync<JobSearchUsageResponse>("/api/job-search/usage", JobSearchTestHost.Json);
        usage!.DailyCreditsUsed.ShouldBe(2);
    }

    [Fact]
    public async Task Cache_Hits_Do_Not_Spend_Credits()
    {
        for (var i = 1; i <= 3; i++)
        {
            (await _alice.GetAsync("/api/job-search/jobs?query=same")).EnsureSuccessStatusCode();
        }

        var again = await _alice.GetFromJsonAsync<JobSearchResultsResponse>("/api/job-search/jobs?query=same", JobSearchTestHost.Json);

        again!.Meta.FromCache.ShouldBeTrue();
        var usage = await _alice.GetFromJsonAsync<JobSearchUsageResponse>("/api/job-search/usage", JobSearchTestHost.Json);
        usage!.DailyCreditsUsed.ShouldBe(1);
    }

    [Fact]
    public async Task One_Users_Day_Does_Not_Touch_Anothers()
    {
        for (var i = 1; i <= 3; i++)
        {
            (await _alice.GetAsync($"/api/job-search/jobs?query=alice{i}")).EnsureSuccessStatusCode();
        }

        (await _bob.GetAsync("/api/job-search/jobs?query=bob1")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_Admin_Override_Raises_One_Users_Ceiling()
    {
        var aliceId = await _host.UserIdAsync("quota.alice@example.com");
        var raised = await _admin.PutAsJsonAsync($"/api/admin/job-search/settings/{aliceId}",
            new UpdateJobSearchLimitsRequest(5, null, null), JobSearchTestHost.Json);
        raised.EnsureSuccessStatusCode();

        for (var i = 1; i <= 5; i++)
        {
            (await _alice.GetAsync($"/api/job-search/jobs?query=raised{i}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await _alice.GetAsync("/api/job-search/jobs?query=raised6")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Shared_Month_Ends_For_Everyone()
    {
        for (var i = 1; i <= 3; i++)
        {
            (await _alice.GetAsync($"/api/job-search/jobs?query=month{i}")).EnsureSuccessStatusCode();
        }

        (await _bob.GetAsync("/api/job-search/jobs?query=month4&numPages=2")).EnsureSuccessStatusCode();
        var response = await _bob.GetAsync("/api/job-search/jobs?query=month5");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldStartWith("This month's job search allowance is used up.");
        _host.Handler.CallCount.ShouldBe(4);
    }

    [Fact]
    public async Task The_Providers_Zero_Remaining_Ends_The_Month_Early()
    {
        _host.Handler.RemainingHeader = 0;
        (await _alice.GetAsync("/api/job-search/jobs?query=last")).EnsureSuccessStatusCode();

        var response = await _bob.GetAsync("/api/job-search/jobs?query=after");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldStartWith("This month's job search allowance is used up.");
        _host.Handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_429_With_Nothing_Left_Reads_As_The_Month_Being_Over()
    {
        _host.Handler.AnswerNext(HttpStatusCode.TooManyRequests, JSearchFixtures.Read("gateway-429.json"), remaining: 0);

        var response = await _alice.GetAsync("/api/job-search/jobs?query=hardlimit");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldStartWith("This month's job search allowance is used up.");
        _host.Handler.CallCount.ShouldBe(1);
    }
}
