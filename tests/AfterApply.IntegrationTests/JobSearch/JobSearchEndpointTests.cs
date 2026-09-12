using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>Each route's happy path and its shape, the search/salary caches, the server-side
/// clamps, and how an upstream failure surfaces and is charged.</summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchEndpointTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private JobSearchTestHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchEndpointTests));
        _client = await _host.RegisterAsync("endpoints@example.com");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Search_Returns_The_Summary_Shape_And_Charges_One_Credit_Per_Page()
    {
        var result = await _client.GetFromJsonAsync<JobSearchResultsResponse>(
            "/api/job-search/jobs?query=developer&numPages=2&datePosted=ThreeDays&workFromHome=true&employmentTypes=FullTime,Intern",
            JobSearchTestHost.Json);

        result!.Jobs.Count.ShouldBe(2);
        result.NextCursor.ShouldBe("cursor-page-2");
        result.Meta.FromCache.ShouldBeFalse();
        result.Meta.CreditsCharged.ShouldBe(2);
        result.Jobs[1].Salary!.Min.ShouldBe(57500);
        result.Jobs[1].ApplyOptions.Count.ShouldBe(2);
        _host.Handler.UrisFor("/search-v2").Single().Query
            .ShouldBe("?query=developer&num_pages=2&country=tr&date_posted=3days&work_from_home=true&employment_types=FULLTIME%2CINTERN");
    }

    [Fact]
    public async Task The_Same_Search_Is_Served_From_The_Cache_Whatever_The_Casing_Or_Spacing()
    {
        (await _client.GetAsync("/api/job-search/jobs?query=Backend%20Developer")).EnsureSuccessStatusCode();
        var cached = await _client.GetFromJsonAsync<JobSearchResultsResponse>(
            "/api/job-search/jobs?query=backend%20%20developer&country=TR", JobSearchTestHost.Json);

        cached!.Meta.FromCache.ShouldBeTrue();
        cached.Meta.CreditsCharged.ShouldBe(0);
        cached.Jobs.Count.ShouldBe(2);
        _host.Handler.UrisFor("/search-v2").Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_Different_Cursor_Is_A_Different_Search()
    {
        (await _client.GetAsync("/api/job-search/jobs?query=paged")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=paged&cursor=cursor-page-2")).EnsureSuccessStatusCode();

        var uris = _host.Handler.UrisFor("/search-v2");
        uris.Count.ShouldBe(2);
        uris[1].Query.ShouldContain("cursor=cursor-page-2");
    }

    [Fact]
    public async Task Num_Pages_Is_Clamped_To_The_Server_Cap()
    {
        var result = await _client.GetFromJsonAsync<JobSearchResultsResponse>(
            "/api/job-search/jobs?query=clamped&numPages=10", JobSearchTestHost.Json);

        result!.Meta.CreditsCharged.ShouldBe(3);
        _host.Handler.UrisFor("/search-v2").Single().Query.ShouldContain("num_pages=3");
    }

    [Fact]
    public async Task Too_Many_Ids_Are_Refused_Before_Anything_Is_Sent()
    {
        var response = await _client.GetAsync("/api/job-search/jobs/details?ids=a,b,c,d,e,f");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldContain("5");
        _host.Handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Salary_Returns_All_Fields_And_Is_Cached_Across_Users()
    {
        var first = await _client.GetFromJsonAsync<JobSearchSalaryEstimatesResponse>(
            "/api/job-search/salary?jobTitle=nodejs%20developer&location=new%20york&locationType=City", JobSearchTestHost.Json);
        var other = await _host.RegisterAsync("salary.other@example.com");
        var second = await other.GetFromJsonAsync<JobSearchSalaryEstimatesResponse>(
            "/api/job-search/salary?jobTitle=NodeJS%20Developer&location=New%20York&locationType=City", JobSearchTestHost.Json);

        first!.Meta.CreditsCharged.ShouldBe(1);
        first.Estimates.Single().MedianSalary.ShouldBe(142629.56);
        first.Estimates.Single().SalariesUpdatedAt.ShouldNotBeNull();
        second!.Meta.FromCache.ShouldBeTrue();
        second.Estimates.Single().PublisherName.ShouldBe("Glassdoor");
        _host.Handler.UrisFor("/estimated-salary").ShouldHaveSingleItem().Query
            .ShouldBe("?job_title=nodejs%20developer&location=new%20york&location_type=CITY");
    }

    [Fact]
    public async Task Company_Salary_Returns_All_Fields()
    {
        var result = await _client.GetFromJsonAsync<JobSearchCompanySalariesResponse>(
            "/api/job-search/company-salary?company=Amazon&jobTitle=software%20developer&yearsOfExperience=FourToSix", JobSearchTestHost.Json);

        result!.Meta.CreditsCharged.ShouldBe(1);
        var salary = result.Salaries.Single();
        salary.Company.ShouldBe("Amazon");
        salary.SalaryCount.ShouldBe(974);
        salary.Confidence.ShouldBe("CONFIDENT");
        _host.Handler.UrisFor("/company-job-salary").Single().Query
            .ShouldBe("?company=Amazon&job_title=software%20developer&years_of_experience=FOUR_TO_SIX");
    }

    [Fact]
    public async Task Usage_Reflects_The_Ledger()
    {
        (await _client.GetAsync("/api/job-search/jobs?query=usage&numPages=2")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=usage&numPages=2")).EnsureSuccessStatusCode();

        var usage = await _client.GetFromJsonAsync<JobSearchUsageResponse>("/api/job-search/usage", JobSearchTestHost.Json);

        usage!.DailyCreditsUsed.ShouldBe(2);
        usage.DailyCreditLimit.ShouldBe(10);
        usage.MonthlyCreditsUsed.ShouldBe(2);
        usage.MonthlyCreditLimit.ShouldBe(180);
        usage.UpstreamRequestsRemaining.ShouldBe(150);
        usage.UpstreamObservedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Anonymous_Callers_Get_401()
    {
        var response = await _host.Factory.CreateClient().GetAsync("/api/job-search/jobs?query=x");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Validation_Problems_Name_The_Parameter()
    {
        var response = await _client.GetAsync("/api/job-search/jobs?query=x&employmentTypes=Freelance");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldContain("EmploymentTypes");
        _host.Handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_Upstream_Outage_Is_A_Coded_400_And_Still_Charged()
    {
        _host.Handler.AnswerNext(HttpStatusCode.ServiceUnavailable, """{"message":"down"}""")
            .AnswerNext(HttpStatusCode.ServiceUnavailable, """{"message":"down"}""");

        var response = await _client.GetAsync("/api/job-search/jobs?query=outage");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("The job data provider could not answer. Try again later.");
        _host.Handler.CallCount.ShouldBe(2);

        // Two attempts reached the gateway, and the meter bills both — so does the ledger.
        var row = await _host.QueryAsync(db => db.JobSearchUsages.SingleAsync());
        row.Succeeded.ShouldBeFalse();
        row.Credits.ShouldBe(2);
        row.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task A_Search_That_Needed_The_Retry_Is_Charged_For_Both_Attempts()
    {
        _host.Handler.AnswerNext(HttpStatusCode.ServiceUnavailable, """{"message":"down"}""");

        var result = await _client.GetFromJsonAsync<JobSearchResultsResponse>("/api/job-search/jobs?query=retried", JobSearchTestHost.Json);

        result!.Jobs.Count.ShouldBe(2);
        result.Meta.CreditsCharged.ShouldBe(1);
        (await _host.QueryAsync(db => db.JobSearchUsages.SingleAsync())).Credits.ShouldBe(2);
        _host.Handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_Rejected_Key_Is_Not_Charged_And_Reads_As_Unavailable()
    {
        _host.Handler.AnswerNext(HttpStatusCode.Forbidden, JSearchFixtures.Read("gateway-403.json"));

        var response = await _client.GetAsync("/api/job-search/jobs?query=forbidden");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("Job search is not available right now.");
        _host.Handler.CallCount.ShouldBe(1);

        var row = await _host.QueryAsync(db => db.JobSearchUsages.SingleAsync());
        row.Credits.ShouldBe(0);
        row.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task The_Providers_Own_400_Reads_As_Rejected()
    {
        _host.Handler.AnswerNext(HttpStatusCode.BadRequest, JSearchFixtures.Read("error-envelope.json"));

        var response = await _client.GetAsync("/api/job-search/jobs?query=rejected");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("The search could not be understood. Change the wording and try again.");
    }

    [Fact]
    public async Task Localised_Errors_Follow_Accept_Language()
    {
        _host.Handler.AnswerNext(HttpStatusCode.Forbidden, JSearchFixtures.Read("gateway-403.json"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/job-search/jobs?query=tr");
        request.Headers.AcceptLanguage.Clear();
        request.Headers.AcceptLanguage.ParseAdd("tr");

        var response = await _client.SendAsync(request);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("İş arama şu anda kullanılamıyor.");
    }

    [Fact]
    public async Task Config_Advertises_The_Feature()
    {
        var config = await _host.Factory.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/config");

        config.GetProperty("jobSearch").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }
}
