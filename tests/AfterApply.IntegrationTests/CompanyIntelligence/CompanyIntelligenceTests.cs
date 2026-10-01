using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyIntelligence;

/// <summary>Flag left at its real appsettings.json default (false). HiddenBelow is overridden to
/// 2 purely so a handful of seeded applications is enough to exercise non-Hidden confidence
/// buckets — it does not affect the Enabled flag itself. The contributor-share guard is lifted
/// for the same reason (most tests seed from one account), and so is the five-person floor; their
/// own tests use variants with the shipped values. Every application is seeded as entered on the
/// day it was sent (<see cref="TestApplications"/>) unless the test is about that rule.</summary>
public sealed class CompanyIntelligenceProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CompanyIntelligence:HiddenBelow", "2");
        builder.UseSetting("CompanyIntelligence:MaxContributorShare", "1");
        builder.UseSetting("CompanyIntelligence:MinimumDistinctContributors", "1");
        builder.UseSetting("ResponseRates:CacheSeconds", "1");
    }
}

public class CompanyIntelligenceTests(ApiHost<CompanyIntelligenceProfile> host) : IClassFixture<ApiHost<CompanyIntelligenceProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    // The flag-off host: used both to assert every endpoint 404s while the flag is off, and (via
    // direct DI, bypassing HTTP) to prove the aggregation pipeline itself is correct even while
    // disabled in prod.
    private WebApplicationFactory<Program> _defaultFactory => host;

    // Same database and thresholds, only Enabled flipped to true: the tests compare what the same
    // data looks like through a disabled vs. an enabled CompanyIntelligence flag.
    private WebApplicationFactory<Program> _enabledFactory =>
        host.Variant("enabled", builder => builder.UseSetting("CompanyIntelligence:Enabled", "true"));

    private HttpClient _client = null!;
    private HttpClient _clientB = null!;
    private HttpClient _enabledClient = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = await CreateAuthenticatedClientAsync(_defaultFactory, "ci.default.a@example.com");
        _clientB = await CreateAuthenticatedClientAsync(_defaultFactory, "ci.default.b@example.com");
        _enabledClient = await CreateAuthenticatedClientAsync(_enabledFactory, "ci.enabled@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "CI", "Test", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<(Guid ApplicationId, Guid CompanyId)> CreateApplicationAsync(
        HttpClient client, string companyName, DateTimeOffset appliedAt, bool enteredOnTheDay = true)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        if (enteredOnTheDay)
        {
            await TestApplications.EnteredOnAsync(host.Services, created!.Id, appliedAt);
        }

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
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-45);

        // Two different registered users applying to the same company — CompanyResolver's
        // find-or-create-by-NormalizedName is what's expected to land both on one CompanyId.
        var (app1, company1) = await CreateApplicationAsync(_client, "Cross User Co", appliedAt);
        await ChangeStatusAsync(_client, app1, ApplicationStatus.Interview, appliedAt.AddDays(3));

        var (app2, company2) = await CreateApplicationAsync(_clientB, "Cross User Co", appliedAt);
        await ChangeStatusAsync(_clientB, app2, ApplicationStatus.Rejected, appliedAt.AddDays(5));

        company1.ShouldBe(company2);

        using var scope = _defaultFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        var result = await service.GetByCompanyIdAsync(company1, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.VeryLow); // 2 apps: >= HiddenBelow(2), < VeryLowBelow(50)
        result.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2);
        result.Metrics.MatureApplications.ShouldBe(2);
        result.Metrics.DistinctContributors.ShouldBe(2);
        result.Metrics.ResponseRate.ShouldBe(100.0);
        result.Metrics.InterviewRate.ShouldBe(50.0);
        result.Metrics.OfferRate.ShouldBe(0.0);
        result.Metrics.GhostingRate.ShouldBe(0.0);
        result.Metrics.AverageResponseTimeDays.ShouldBe(4.0);
        result.Metrics.MedianResponseTimeDays.ShouldBe(4.0);
        result.Metrics.ClosureRate.ShouldBe(50.0); // 1 Rejected out of 2 (Interview isn't a closure)
        // responsiveness=100, responseTimeScore=(1 - 4/30)*100=86.7, closureRate=50 → mean=78.9
        result.Metrics.CandidateExperienceScore.ShouldBe(78.9);
    }

    [Fact]
    public async Task Ghosted_And_Withdrawn_Applications_Do_Not_Count_Toward_Closure_Rate()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);

        // Rejected is the only company-given outcome among the three; Ghosted has no company
        // action at all and Withdrawn is the candidate's own decision — neither should inflate
        // Closure Rate the way TerminalApplicationStatuses (a different, broader concept) would.
        var (appRejected, companyId) = await CreateApplicationAsync(_client, "Closure Rate Co", appliedAt);
        await ChangeStatusAsync(_client, appRejected, ApplicationStatus.Rejected, appliedAt.AddDays(2));

        var (appGhosted, companyId2) = await CreateApplicationAsync(_client, "Closure Rate Co", appliedAt);
        await ChangeStatusAsync(_client, appGhosted, ApplicationStatus.Ghosted, appliedAt.AddDays(35));
        companyId2.ShouldBe(companyId);

        var (appWithdrawn, companyId3) = await CreateApplicationAsync(_client, "Closure Rate Co", appliedAt);
        await ChangeStatusAsync(_client, appWithdrawn, ApplicationStatus.Withdrawn, appliedAt.AddDays(1));
        companyId3.ShouldBe(companyId);

        using var scope = _defaultFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        var result = await service.GetByCompanyIdAsync(companyId, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(3);
        result.Metrics.ClosureRate.ShouldBe(33.3); // only the Rejected one counts, not Ghosted/Withdrawn
    }

    [Fact]
    public async Task GetByCompanyId_Returns_Hidden_With_Null_Metrics_Below_Threshold()
    {
        var (_, companyId) = await CreateApplicationAsync(_client, "Single App Co", DateTimeOffset.UtcNow.AddDays(-2));

        using var scope = _defaultFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        var result = await service.GetByCompanyIdAsync(companyId, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden); // 1 app < HiddenBelow(2)
        result.Metrics.ShouldBeNull();
    }

    [Fact]
    public async Task Endpoint_Returns_Ok_With_Metrics_When_Flag_Enabled_And_Above_Threshold()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);
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
    public async Task Applications_Older_Than_The_Window_Do_Not_Count()
    {
        // An unbounded aggregate gives a company no way to ever improve: whatever it did two years
        // ago would stay in its number forever. Two applications inside the window, two outside —
        // only the recent pair may be counted, and the response has to say which period that is.
        var (_, companyId) = await CreateApplicationAsync(_enabledClient, "Windowed Co", DateTimeOffset.UtcNow.AddDays(-20));
        await CreateApplicationAsync(_enabledClient, "Windowed Co", DateTimeOffset.UtcNow.AddDays(-60));
        await CreateApplicationAsync(_enabledClient, "Windowed Co", DateTimeOffset.UtcNow.AddMonths(-18));
        await CreateApplicationAsync(_enabledClient, "Windowed Co", DateTimeOffset.UtcNow.AddYears(-3));

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result!.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2);

        result.WindowEnd.ShouldBeGreaterThan(result.WindowStart);
        (result.WindowEnd - result.WindowStart).TotalDays.ShouldBeGreaterThan(300);
    }

    [Fact]
    public async Task The_Window_Is_Reported_Even_When_The_Company_Is_Hidden()
    {
        // "Fewer than the threshold in this period" and "fewer ever" are different claims, and only
        // the first one is true — so the period travels with the Hidden answer too.
        var (_, companyId) = await CreateApplicationAsync(_enabledClient, "Hidden Window Co", DateTimeOffset.UtcNow.AddDays(-3));

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden);
        result.Metrics.ShouldBeNull();
        result.WindowEnd.ShouldBeGreaterThan(result.WindowStart);
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

    [Fact]
    public async Task Endpoint_Answers_Without_A_Token_When_Flag_Enabled()
    {
        // The company page reads this the way it reads reviews: no account. The thresholds are
        // the guard, not sign-in.
        var (_, companyId) = await CreateApplicationAsync(_enabledClient, "Anonymous Co", DateTimeOffset.UtcNow.AddDays(-40));
        await CreateApplicationAsync(_enabledClient, "Anonymous Co", DateTimeOffset.UtcNow.AddDays(-40));

        var anonymous = _enabledFactory.CreateClient();
        var response = await anonymous.GetAsync($"/api/company-intelligence/{companyId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);
        result!.Metrics.ShouldNotBeNull();
        result.Thresholds.HiddenBelow.ShouldBe(2);
        result.Thresholds.MaturityDays.ShouldBe(30);
    }

    [Fact]
    public async Task Applications_Too_Young_To_Be_Answered_Count_Toward_The_Total_But_Not_The_Rates()
    {
        // Two answered applications from six weeks ago and one three-day-old application nobody
        // has had time to answer: the young one is in the count (3) but not in any rate, so the
        // response rate stays 100 instead of dropping to 66.7 for no fault of the company's.
        var old = DateTimeOffset.UtcNow.AddDays(-45);
        var (app1, companyId) = await CreateApplicationAsync(_enabledClient, "Maturity Co", old);
        await ChangeStatusAsync(_enabledClient, app1, ApplicationStatus.Rejected, old.AddDays(4));
        var (app2, _) = await CreateApplicationAsync(_enabledClient, "Maturity Co", old);
        await ChangeStatusAsync(_enabledClient, app2, ApplicationStatus.Screening, old.AddDays(6));
        await CreateApplicationAsync(_enabledClient, "Maturity Co", DateTimeOffset.UtcNow.AddDays(-3));

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result!.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(3);
        result.Metrics.MatureApplications.ShouldBe(2);
        result.Metrics.ResponseRate.ShouldBe(100.0);
        result.Metrics.GhostingRate.ShouldBe(0.0);
        result.Metrics.MedianResponseTimeDays.ShouldBe(5.0);
    }

    [Fact]
    public async Task Post_Interview_Silence_Is_Reported_Separately_From_Ghosting()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-60);
        var (interviewedThenSilent, companyId) = await CreateApplicationAsync(_enabledClient, "Silence Co", old);
        await ChangeStatusAsync(_enabledClient, interviewedThenSilent, ApplicationStatus.Interview, old.AddDays(5));
        await ChangeStatusAsync(_enabledClient, interviewedThenSilent, ApplicationStatus.Ghosted, old.AddDays(40));
        var (interviewedThenRejected, _) = await CreateApplicationAsync(_enabledClient, "Silence Co", old);
        await ChangeStatusAsync(_enabledClient, interviewedThenRejected, ApplicationStatus.Interview, old.AddDays(5));
        await ChangeStatusAsync(_enabledClient, interviewedThenRejected, ApplicationStatus.Rejected, old.AddDays(12));
        var (neverAnswered, _) = await CreateApplicationAsync(_enabledClient, "Silence Co", old);
        await ChangeStatusAsync(_enabledClient, neverAnswered, ApplicationStatus.Ghosted, old.AddDays(40));

        var response = await _enabledClient.GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result!.Metrics.ShouldNotBeNull();
        result.Metrics!.PostInterviewSilenceRate.ShouldBe(50.0); // 1 of the 2 interviewed
        result.Metrics.GhostingRate.ShouldBe(66.7); // 2 of 3 overall
    }

    [Fact]
    public async Task One_Person_Being_Most_Of_The_Sample_Hides_The_Company_Like_A_Small_Count_Would()
    {
        // The shipped guard: a third. Four applications, three from one account — the count clears
        // the (test) ladder, the people do not, and the answer is the same Hidden as a small count.
        var guarded = host.Variant("guarded", builder =>
        {
            builder.UseSetting("CompanyIntelligence:Enabled", "true");
            builder.UseSetting("CompanyIntelligence:MaxContributorShare", "0.3334");
        });
        var (clientA, _) = await host.RegisterAsync("ci.guard.a@example.com", on: guarded);
        var (clientB, _) = await host.RegisterAsync("ci.guard.b@example.com", on: guarded);
        var old = DateTimeOffset.UtcNow.AddDays(-40);

        var (_, companyId) = await CreateApplicationAsync(clientA, "Dominated Co", old);
        await CreateApplicationAsync(clientA, "Dominated Co", old);
        await CreateApplicationAsync(clientA, "Dominated Co", old);
        await CreateApplicationAsync(clientB, "Dominated Co", old);

        var response = await guarded.CreateClient().GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden);
        result.Metrics.ShouldBeNull();
        result.Thresholds.MaxContributorSharePercent.ShouldBe(33);
    }

    [Fact]
    public async Task Sector_Comparison_Travels_With_A_Hidden_Company_When_Its_Industry_Is_Known()
    {
        // The company is below its own threshold, but its sector is not below the sector page's
        // — the "not yet, but here is the wider picture" state.
        var sectorHost = host.Variant("sector", builder =>
        {
            builder.UseSetting("CompanyIntelligence:Enabled", "true");
            builder.UseSetting("CompanyIntelligence:HiddenBelow", "50");
            builder.UseSetting("ResponseRates:MinimumContributors", "2");
            builder.UseSetting("ResponseRates:MinimumApplications", "3");
            builder.UseSetting("ResponseRates:CacheSeconds", "1");
        });
        var (clientA, _) = await host.RegisterAsync("ci.sector.a@example.com", on: sectorHost);
        var (clientB, _) = await host.RegisterAsync("ci.sector.b@example.com", on: sectorHost);
        var (clientC, _) = await host.RegisterAsync("ci.sector.c@example.com", on: sectorHost);
        var old = DateTimeOffset.UtcNow.AddDays(-40);

        // Three people, one application each: clears the sector's people and count floors (2 / 3
        // on this host) and its half-share guard, while staying far under the company's 50.
        var (_, companyId) = await CreateApplicationAsync(clientA, "Sector Software Co", old);
        await CreateApplicationAsync(clientB, "Sector Software Co", old);
        await CreateApplicationAsync(clientC, "Sector Software Co", old);
        using (var scope = sectorHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.SingleAsync(c => c.Id == companyId);
            company.EnrichFrom(Source.LinkedIn, null, "Software Development", null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var response = await sectorHost.CreateClient().GetAsync($"/api/company-intelligence/{companyId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions);

        result!.Confidence.ShouldBe(ConfidenceBucket.Hidden);
        result.SectorComparison.ShouldNotBeNull();
        result.SectorComparison!.Sector.ShouldBe(AfterApply.Domain.Benchmark.BenchmarkSector.SoftwareAndIt);
        result.SectorComparison.Figures.ShouldNotBeNull();
        result.SectorComparison.Figures!.Applications.ShouldBe(3);
        result.SectorComparison.Figures.Contributors.ShouldBe(3);
    }

    // ---- which applications count (AggregateEligibility, DECISIONS.md 2026-10-01) --------------

    private async Task<CompanyIntelligenceResponse> ComputeAsync(Guid companyId)
    {
        using var scope = _defaultFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICompanyIntelligenceService>();
        return (await service.GetByCompanyIdAsync(companyId, CancellationToken.None))!;
    }

    [Fact]
    public async Task Applications_Entered_More_Than_A_Week_After_Applying_Do_Not_Count()
    {
        // The import case: months of history logged on one day. A week late is still "as it
        // happened"; eight days is not.
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-45);
        var (_, companyId) = await CreateApplicationAsync(_client, "Fresh Entry Co", appliedAt);
        var (onTheEdge, _) = await CreateApplicationAsync(_client, "Fresh Entry Co", appliedAt, enteredOnTheDay: false);
        await TestApplications.EnteredOnAsync(host.Services, onTheEdge, appliedAt.AddDays(7));
        var (tooLate, _) = await CreateApplicationAsync(_client, "Fresh Entry Co", appliedAt, enteredOnTheDay: false);
        await TestApplications.EnteredOnAsync(host.Services, tooLate, appliedAt.AddDays(8));
        await CreateApplicationAsync(_client, "Fresh Entry Co", appliedAt, enteredOnTheDay: false); // entered today

        var result = await ComputeAsync(companyId);

        result.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2);
    }

    [Fact]
    public async Task An_Account_That_Turned_Contribution_Off_Leaves_The_Figures_And_Can_Come_Back()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);
        var (_, companyId) = await CreateApplicationAsync(_client, "Opt Out Co", appliedAt);
        await CreateApplicationAsync(_client, "Opt Out Co", appliedAt);
        await CreateApplicationAsync(_clientB, "Opt Out Co", appliedAt);
        (await ComputeAsync(companyId)).Metrics!.TotalApplications.ShouldBe(3);

        var off = await _clientB.PutAsJsonAsync("/api/users/me/aggregate-contribution",
            new UpdateAggregateContributionRequest(false), JsonOptions);
        off.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await off.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions))!.ContributesToAggregates.ShouldBeFalse();

        var withoutB = await ComputeAsync(companyId);
        withoutB.Metrics!.TotalApplications.ShouldBe(2);
        withoutB.Metrics.DistinctContributors.ShouldBe(1);

        // Only the caller's own row moved: the other account still contributes.
        var me = await _client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        me!.ContributesToAggregates.ShouldBeTrue();

        var on = await _clientB.PutAsJsonAsync("/api/users/me/aggregate-contribution",
            new UpdateAggregateContributionRequest(true), JsonOptions);
        on.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ComputeAsync(companyId)).Metrics!.TotalApplications.ShouldBe(3);
    }

    [Fact]
    public async Task The_Contribution_Switch_Needs_A_Signed_In_Caller()
    {
        var response = await _defaultFactory.CreateClient().PutAsJsonAsync("/api/users/me/aggregate-contribution",
            new UpdateAggregateContributionRequest(false), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Open_Applications_Of_Someone_Who_Stopped_Tracking_Leave_The_Figures_Finished_Ones_Stay()
    {
        // Left at Applied by someone who has not signed in for 100 days, an application reads as
        // "the company never answered" — the one error this figure must not make. The rejection
        // that same person recorded is still a recorded outcome.
        var (lapsed, _) = await host.RegisterAsync("ci.lapsed@example.com");
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-150);
        var (_, companyId) = await CreateApplicationAsync(_client, "Lapsed Tracker Co", appliedAt);
        await CreateApplicationAsync(lapsed, "Lapsed Tracker Co", appliedAt);
        var (rejected, _) = await CreateApplicationAsync(lapsed, "Lapsed Tracker Co", appliedAt);
        await ChangeStatusAsync(lapsed, rejected, ApplicationStatus.Rejected, appliedAt.AddDays(6));

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await db.Users.Where(u => u.Email == "ci.lapsed@example.com").Select(u => u.Id).SingleAsync();
            await db.RefreshTokens.Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.CreatedAt, DateTimeOffset.UtcNow.AddDays(-100)));
        }

        var result = await ComputeAsync(companyId);

        result.Metrics.ShouldNotBeNull();
        result.Metrics!.TotalApplications.ShouldBe(2); // the active user's open one + the lapsed user's rejection
        result.Metrics.ResponseRate.ShouldBe(50.0);
    }

    /// <summary>Seeds <paramref name="perPerson"/> finished (Rejected) applications for each of
    /// <paramref name="people"/> fresh accounts, straight into the database — twenty-odd rows
    /// through the API would be most of this class's time budget. Finished rows count whether or
    /// not their owner has a session, so no sign-in is needed.</summary>
    private async Task SeedRejectedAsync(Guid companyId, string prefix, int people, int perPerson, DateTimeOffset appliedAt)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < people; i++)
        {
            var email = $"{prefix}-{i}@example.com";
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, CreatedAt = appliedAt };
            (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();

            for (var j = 0; j < perPerson; j++)
            {
                var application = Domain.Applications.Application.Create(user.Id, companyId, "Engineer", null, null,
                    EmploymentType.FullTime, appliedAt, Source.Manual, notes: null, now: appliedAt);
                application.ChangeStatus(ApplicationStatus.Rejected, appliedAt.AddDays(5),
                    new StatusChangeContext(Source.Manual, StatusChangeOrigin.Manual));
                db.Applications.Add(application);
            }
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_Shipped_Floor_Hides_Twenty_Applications_From_Four_People_And_Shows_Them_From_Five()
    {
        var shipped = host.Variant("shipped-floor", builder =>
        {
            builder.UseSetting("CompanyIntelligence:Enabled", "true");
            builder.UseSetting("CompanyIntelligence:HiddenBelow", "20");
            builder.UseSetting("CompanyIntelligence:MinimumDistinctContributors", "5");
            builder.UseSetting("CompanyIntelligence:MaxContributorShare", "0.3334");
        });
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-60);
        var (_, companyId) = await CreateApplicationAsync(_client, "Floor Co", appliedAt);
        await SeedRejectedAsync(companyId, "ci.floor.four", people: 3, perPerson: 6, appliedAt); // 19 applications, 4 people

        async Task<CompanyIntelligenceResponse> ReadAsync()
        {
            var response = await shipped.CreateClient().GetAsync($"/api/company-intelligence/{companyId}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions))!;
        }

        (await ReadAsync()).Confidence.ShouldBe(ConfidenceBucket.Hidden); // 19 < 20

        await CreateApplicationAsync(_client, "Floor Co", appliedAt); // 20 applications, still 4 people
        var fourPeople = await ReadAsync();
        fourPeople.Confidence.ShouldBe(ConfidenceBucket.Hidden);
        fourPeople.Metrics.ShouldBeNull();
        fourPeople.Thresholds.MinimumContributors.ShouldBe(5);

        await SeedRejectedAsync(companyId, "ci.floor.fifth", people: 1, perPerson: 1, appliedAt); // 21, 5 people
        var fivePeople = await ReadAsync();
        fivePeople.Confidence.ShouldBe(ConfidenceBucket.VeryLow);
        fivePeople.Metrics!.DistinctContributors.ShouldBe(5);
        fivePeople.Metrics.TotalApplications.ShouldBe(21);
    }
}
