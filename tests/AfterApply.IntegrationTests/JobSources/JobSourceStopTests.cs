using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Documents;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// The two ways the sweep stops itself — the source saying no, and the day's budget running out —
/// and that both are visible on the admin usage endpoint. The "low volume" promise, as tests.
/// </summary>
/// <summary>LinkedIn alone (these tests are about one source's budget and stop rule), with a
/// small daily budget and a clock the tests move.</summary>
public sealed class JobSourceStopProfile : IHostProfile
{
    public LinkedInStubHandler LinkedIn { get; } = new();
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("JobSources:Enabled", "true");
        builder.UseSetting("JobSources:KariyerNetEnabled", "false");
        builder.UseSetting("JobSources:MinDelayMs", "0");
        builder.UseSetting("JobSources:RetryBaseDelayMs", "1");
        builder.UseSetting("JobSources:MaxRequestsPerDay", JobSourceStopTests.DailyBudget.ToString());
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient(nameof(ILinkedInJobSourceClient))
                .ConfigurePrimaryHttpMessageHandler(() => LinkedIn);
        });
    }

    public void Reset()
    {
        LinkedIn.Reset();
        Clock.Reset();
    }
}

[Collection(IntegrationTestCollection.Name)]
public class JobSourceStopTests(ApiHost<JobSourceStopProfile> host) : IClassFixture<ApiHost<JobSourceStopProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    internal const int DailyBudget = 3;

    private WebApplicationFactory<Program> _factory => host;
    private LinkedInStubHandler _linkedIn => host.Profile.LinkedIn;
    private MutableTimeProvider _clock => host.Profile.Clock;
    private HttpClient _admin = null!;
    private HttpClient _pro = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _admin = await RegisterAsync("admin.stop@ekariyerim.com");
        _pro = await RegisterAsync("pro.stop@example.com");

        Guid proId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(u => u.Email == "admin.stop@ekariyerim.com")).IsAdmin = true;
            proId = await db.Users.Where(u => u.Email == "pro.stop@example.com").Select(u => u.Id).SingleAsync();
            db.CvDocuments.Add(CvDocument.Create(proId, "cv.pdf", CvFileFormat.Pdf, 1234, true, _clock.GetUtcNow()));
            await db.SaveChangesAsync();
        }

        (await _admin.PutAsJsonAsync($"/api/admin/pro/entitlements/{proId}",
            new GrantProEntitlementRequest(_clock.GetUtcNow().AddMonths(1)), JsonOptions)).EnsureSuccessStatusCode();
        (await _pro.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest([".NET Developer", "Java Developer"], "İstanbul", AcceptAiScoring: true), JsonOptions)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_429_Stops_The_Whole_Run_And_The_Next_Day_Is_Skipped()
    {
        _linkedIn.AnswerNextWith(HttpStatusCode.TooManyRequests);

        await SweepAsync();

        // One request, answered 429: no retry, no second query, no detail fetches.
        _linkedIn.Requested.Count.ShouldBe(1);
        (await ListAsync()).Items.ShouldBeEmpty();

        var usage = await _admin.GetFromJsonAsync<JobSourceUsageResponse>("/api/admin/job-sources/usage", JsonOptions);
        usage!.RequestsToday.ShouldBe(1);
        usage.MaxRequestsPerDay.ShouldBe(DailyBudget);
        usage.LastBlockedAt.ShouldNotBeNull();
        usage.CooldownUntil.ShouldBe(usage.LastBlockedAt.Value.AddHours(24));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fetch = await db.JobSourceFetches.SingleAsync();
            fetch.Outcome.ShouldBe(JobSourceFetchOutcome.RateLimited);
            fetch.StatusCode.ShouldBe(429);
            // The query was not marked as run: next time it is tried again.
            (await db.JobSourceQueries.AllAsync(q => q.LastRunAt == null)).ShouldBeTrue();
        }

        // An hour later the cooldown still holds: not a single request.
        _clock.Advance(TimeSpan.FromHours(1));
        await SweepAsync();
        _linkedIn.Requested.Count.ShouldBe(1);

        // A day later it is over — and the budget has reset with the day.
        _clock.Advance(TimeSpan.FromHours(24));
        await SweepAsync();
        _linkedIn.Requested.Count.ShouldBeGreaterThan(1);
        (await _admin.GetFromJsonAsync<JobSourceUsageResponse>("/api/admin/job-sources/usage", JsonOptions))!.CooldownUntil.ShouldBeNull();
    }

    [Fact]
    public async Task A_5xx_Is_Retried_Inside_The_Same_Request_Budget_Row_And_Does_Not_Stop_The_Run()
    {
        _linkedIn.AnswerNextWith(HttpStatusCode.BadGateway);

        await SweepAsync();

        // The 502 was retried by the pipeline (2 transport calls, 1 ledger row) and the run went on.
        (await ListAsync()).Items.ShouldNotBeEmpty();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.JobSourceFetches.CountAsync(f => f.Outcome == JobSourceFetchOutcome.Ok)).ShouldBe(DailyBudget);
        (await db.JobSourceFetches.CountAsync()).ShouldBe(DailyBudget);
    }

    [Fact]
    public async Task The_Daily_Budget_Caps_Requests_Across_Queries_And_Details()
    {
        await SweepAsync();

        // Three requests a day: two pages of the first query and the first page of the second —
        // the details, which come last, got nothing.
        _linkedIn.SearchRequests.ShouldBe(DailyBudget);
        _linkedIn.PostingRequests.ShouldBe(0);
        var list = await ListAsync();
        list.Items.Count.ShouldBe(13);
        list.Items.ShouldAllBe(i => i.Seniority == null);

        var usage = await _admin.GetFromJsonAsync<JobSourceUsageResponse>("/api/admin/job-sources/usage", JsonOptions);
        usage!.RequestsToday.ShouldBe(DailyBudget);
        usage.CooldownUntil.ShouldBeNull();
        usage.ActiveQueryCount.ShouldBe(2);

        // Tomorrow: both queries are marked run for the week (the second got one page before the
        // budget cut it off, and one page is a run), but the details are still owed and now get
        // the day's budget. Not on a Sunday: tomorrow is a new week then, and the queries run again
        // (see MutableTimeProvider.StaysInIsoWeek).
        if (!_clock.StaysInIsoWeek(TimeSpan.FromHours(24))) return;
        _clock.Advance(TimeSpan.FromHours(24));
        await SweepAsync();
        _linkedIn.PostingRequests.ShouldBe(DailyBudget);
    }

    private async Task SweepAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobSourceSweepService>().SweepAsync(CancellationToken.None);
    }

    private async Task<JobSourceDeliveriesResponse> ListAsync() =>
        (await _pro.GetFromJsonAsync<JobSourceDeliveriesResponse>("/api/job-sources/postings", JsonOptions))!;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Stop", "Test", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
