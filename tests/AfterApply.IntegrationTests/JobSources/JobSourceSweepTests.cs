using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// The weekly job-source sweep end to end against a stubbed LinkedIn: who it runs for, that a
/// shared query is fetched once, what each user is and is not handed, the weekly cap, and how
/// the run reads back through the API. The sweep is invoked directly through its service — it
/// has no endpoint, on purpose — with the clock under test control.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSourceSweepTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // The clock under test starts at the real "now" and only ever moves forward: access tokens
    // carry a not-before stamped from the same TimeProvider, and the JWT middleware checks them
    // against the wall clock — a fake clock in the past or a login after an advance would not
    // authenticate.
    private static readonly DateTimeOffset RunMoment = DateTimeOffset.UtcNow;
    private static readonly int ThisWeek = WeekKey.From(RunMoment);
    private static readonly int NextWeek = WeekKey.From(RunMoment.AddDays(7));

    private WebApplicationFactory<Program>? _factory;
    private LinkedInStubHandler _linkedIn = null!;
    private MutableTimeProvider _clock = null!;
    private HttpClient _admin = null!;
    private HttpClient _pro1 = null!;
    private HttpClient _pro2 = null!;
    private HttpClient _free = null!;
    private Guid _pro1Id;
    private Guid _pro2Id;
    private Guid _freeId;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(JobSourceSweepTests));
        _linkedIn = new LinkedInStubHandler();
        _clock = new MutableTimeProvider(RunMoment);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("JobSources:Enabled", "true");
            builder.UseSetting("JobSources:MinDelayMs", "0");
            builder.UseSetting("JobSources:RetryBaseDelayMs", "1");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(_clock);
                services.AddHttpClient(nameof(ILinkedInJobSourceClient))
                    .ConfigurePrimaryHttpMessageHandler(() => _linkedIn);
            });
        });

        (_admin, _) = await RegisterAsync("admin.jobsources@ekariyerim.com");
        (_pro1, _pro1Id) = await RegisterAsync("pro1.jobsources@example.com");
        (_pro2, _pro2Id) = await RegisterAsync("pro2.jobsources@example.com");
        (_free, _freeId) = await RegisterAsync("free.jobsources@example.com");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "admin.jobsources@ekariyerim.com");
            admin.IsAdmin = true;
            // A CV is a precondition of being swept (there is nothing to match without one); the
            // row is enough, the bytes are never read here.
            foreach (var userId in new[] { _pro1Id, _pro2Id, _freeId })
            {
                db.CvDocuments.Add(CvDocument.Create(userId, "cv.pdf", CvFileFormat.Pdf, 1234, true, RunMoment));
            }

            await db.SaveChangesAsync();
        }

        foreach (var userId in new[] { _pro1Id, _pro2Id })
        {
            (await _admin.PutAsJsonAsync($"/api/admin/pro/entitlements/{userId}",
                new GrantProEntitlementRequest(RunMoment.AddMonths(1)), JsonOptions)).EnsureSuccessStatusCode();
        }

        foreach (var client in new[] { _pro1, _pro2, _free })
        {
            (await client.PutAsJsonAsync("/api/job-sources/profile",
                new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul"), JsonOptions)).EnsureSuccessStatusCode();
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await TestHostDisposal.DisposeQuietlyAsync(_factory);
        }
    }

    [Fact]
    public async Task A_Shared_Query_Is_Fetched_Once_And_Both_Paying_Users_Get_The_Postings()
    {
        await SweepAsync();

        // Two users, one query, two pages (10 + 3 cards): two search requests, not four.
        _linkedIn.SearchRequests.ShouldBe(2);
        // Every delivered posting got its description, once — 13 distinct postings.
        _linkedIn.PostingRequests.ShouldBe(13);

        var pro1 = await ListAsync(_pro1);
        var pro2 = await ListAsync(_pro2);
        pro1.Items.Count.ShouldBe(13);
        pro2.Items.Count.ShouldBe(13);
        pro1.Run.ShouldNotBeNull();
        pro1.Run.WeekKey.ShouldBe(ThisWeek);
        pro1.Run.CandidateCount.ShouldBe(13);
        pro1.Run.DeliveredCount.ShouldBe(13);
        pro1.Run.ExcludedAppliedCount.ShouldBe(0);

        // Best rank first, and the detail is there.
        pro1.Items[0].Title.ShouldBe("Software Developer 1");
        pro1.Items[0].Seniority.ShouldBe("Mid-Senior level");
        var detail = await _pro1.GetFromJsonAsync<JobSourcePostingDetailResponse>($"/api/job-sources/postings/{pro1.Items[0].Id}", JsonOptions);
        detail!.Description.ShouldBe("Description of posting 4460000001.\nC#\n.NET");
        detail.Url.ShouldBe("https://www.linkedin.com/jobs/view/4460000001");
    }

    [Fact]
    public async Task Users_Who_Are_Not_Paying_Or_Have_No_Cv_Or_Are_Inactive_Are_Not_Swept()
    {
        // pro2 loses the CV; pro1 has not signed in for a month; free never paid.
        await using (var scope = _factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.CvDocuments.Where(c => c.UserId == _pro2Id).ExecuteDeleteAsync();
        }

        // Everyone's last sign-in is now a month old; pro2 gets a fresh one but no CV.
        _clock.Advance(TimeSpan.FromDays(31));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recent = await db.RefreshTokens.Where(t => t.UserId == _pro2Id).OrderByDescending(t => t.CreatedAt).FirstAsync();
            db.RefreshTokens.Add(AfterApply.Infrastructure.Identity.RefreshToken.Create(_pro2Id, "fresh-" + recent.TokenHash,
                _clock.GetUtcNow().AddDays(30), _clock.GetUtcNow(), null));
            await db.SaveChangesAsync();
        }

        await SweepAsync();

        _linkedIn.SearchRequests.ShouldBe(0);
        (await ListAsync(_pro1)).Run.ShouldBeNull();
        (await ListAsync(_pro2)).Run.ShouldBeNull();
        (await ListAsync(_free)).Run.ShouldBeNull();
    }

    [Fact]
    public async Task Postings_The_User_Already_Applied_To_Are_Left_Out_And_Counted()
    {
        // Two ways an application can carry the LinkedIn id: through the extension, which
        // resolves the URL into a Job with Source=LinkedIn and ExternalId...
        (await _pro1.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Bankacılık", "Software Developer 1",
                "https://www.linkedin.com/jobs/view/4460000001/?refId=abc", "İstanbul", null, null, null, null),
            JsonOptions)).EnsureSuccessStatusCode();

        // ...and a hand-entered one whose only trace of the id is the URL on the application row.
        await using (var scope = _factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.FirstAsync();
            db.Applications.Add(DomainApplication.Create(_pro1Id, company.Id, "Software Developer 3",
                "https://tr.linkedin.com/jobs/view/software-developer-3-at-acme-4460000003", "İstanbul",
                EmploymentType.FullTime, RunMoment, Source.Manual, null, RunMoment));
            await db.SaveChangesAsync();
        }

        await SweepAsync();

        var pro1 = await ListAsync(_pro1);
        pro1.Items.Count.ShouldBe(11);
        pro1.Items.Select(i => i.Url).ShouldNotContain("https://www.linkedin.com/jobs/view/4460000001");
        pro1.Items.Select(i => i.Url).ShouldNotContain("https://www.linkedin.com/jobs/view/4460000003");
        pro1.Run!.CandidateCount.ShouldBe(13);
        pro1.Run.DeliveredCount.ShouldBe(11);
        pro1.Run.ExcludedAppliedCount.ShouldBe(2);
        pro1.Run.ExcludedRecentlyShownCount.ShouldBe(0);

        // The other user did not apply: the same postings reach them.
        var pro2 = await ListAsync(_pro2);
        pro2.Items.Count.ShouldBe(13);
        pro2.Run!.ExcludedAppliedCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_Weekly_Cap_Is_Per_User_And_A_Second_Run_In_The_Same_Week_Adds_Nothing()
    {
        (await _admin.PutAsJsonAsync($"/api/admin/job-sources/settings/{_pro2Id}",
            new UpdateUserJobSourceLimitsRequest(5), JsonOptions)).EnsureSuccessStatusCode();
        var settings = await _admin.GetFromJsonAsync<UserJobSourceSettingsResponse>($"/api/admin/job-sources/settings/{_pro2Id}", JsonOptions);
        settings!.WeeklyPostingLimit.ShouldBe(5);
        settings.EffectiveWeeklyPostingLimit.ShouldBe(5);
        (await _admin.GetFromJsonAsync<UserJobSourceSettingsResponse>($"/api/admin/job-sources/settings/{_pro1Id}", JsonOptions))!
            .EffectiveWeeklyPostingLimit.ShouldBe(50);

        await SweepAsync();

        (await ListAsync(_pro1)).Items.Count.ShouldBe(13);
        var pro2 = await ListAsync(_pro2);
        pro2.Items.Count.ShouldBe(5);
        pro2.Run!.DeliveredCount.ShouldBe(5);
        pro2.Run.CandidateCount.ShouldBe(13);
        // Only what was delivered gets a description: 13 for pro1 (which covers pro2's 5).
        _linkedIn.PostingRequests.ShouldBe(13);

        // The same week again — a late or repeated job: nothing new, and LinkedIn is not asked again.
        // (An hour, not days: the clock started at the real "now", which may be a Sunday.)
        _clock.Advance(TimeSpan.FromHours(1));
        await SweepAsync();

        _linkedIn.SearchRequests.ShouldBe(2);
        (await ListAsync(_pro2)).Items.Count.ShouldBe(5);
        (await ListAsync(_pro1)).Run!.DeliveredCount.ShouldBe(13);
    }

    [Fact]
    public async Task Next_Week_The_Query_Runs_Again_And_Only_New_Postings_Are_Delivered()
    {
        await SweepAsync();
        (await ListAsync(_pro1)).Items.Count.ShouldBe(13);

        _clock.Advance(TimeSpan.FromDays(7));
        _linkedIn.Cards.Insert(0, ("4460000099", "Brand New Posting", "Kuzey Teknoloji", "İstanbul", "2026-09-20"));
        await SweepAsync();

        _linkedIn.SearchRequests.ShouldBe(4);
        var thisWeek = await ListAsync(_pro1);
        thisWeek.Run!.WeekKey.ShouldBe(NextWeek);
        thisWeek.Items.Select(i => i.Title).ShouldBe(["Brand New Posting"]);
        thisWeek.Run.CandidateCount.ShouldBe(14);
        thisWeek.Run.DeliveredCount.ShouldBe(1);
        thisWeek.Run.ExcludedRecentlyShownCount.ShouldBe(13);

        // Last week is still readable by its key.
        var lastWeek = await ListAsync(_pro1, ThisWeek);
        lastWeek.Items.Count.ShouldBe(13);
        lastWeek.Run!.WeekKey.ShouldBe(ThisWeek);
    }

    [Fact]
    public async Task A_Posting_Is_Only_Readable_By_A_User_It_Was_Delivered_To()
    {
        (await _admin.PutAsJsonAsync($"/api/admin/job-sources/settings/{_pro2Id}",
            new UpdateUserJobSourceLimitsRequest(1), JsonOptions)).EnsureSuccessStatusCode();
        await SweepAsync();

        var pro1 = await ListAsync(_pro1);
        var notDeliveredToPro2 = pro1.Items[5].Id;

        (await _pro2.GetAsync($"/api/job-sources/postings/{notDeliveredToPro2}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _free.GetAsync($"/api/job-sources/postings/{notDeliveredToPro2}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _pro1.GetAsync($"/api/job-sources/postings/{notDeliveredToPro2}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Routes_Refuse_Ordinary_Users_And_The_Profile_Is_Validated()
    {
        (await _pro1.GetAsync($"/api/admin/job-sources/settings/{_pro1Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _pro1.PutAsJsonAsync($"/api/admin/job-sources/settings/{_pro1Id}", new UpdateUserJobSourceLimitsRequest(500), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _pro1.PutAsJsonAsync($"/api/admin/pro/entitlements/{_freeId}", new GrantProEntitlementRequest(RunMoment.AddYears(1)), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _pro1.GetAsync("/api/admin/job-sources/usage")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await _pro1.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest(["a", "b", "c", "d"], "İstanbul"), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _pro1.GetAsync("/api/job-sources/postings?week=202600")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var profile = await _pro1.GetFromJsonAsync<JobSourceProfileResponse>("/api/job-sources/profile", JsonOptions);
        profile!.Titles.ShouldBe([".NET Developer"]);
        profile.Location.ShouldBe("İstanbul");

        // Re-saving with the same title kept and a new one in front: order is the user's, the
        // shared query behind ".NET Developer" is reused, and a duplicate spelling collapses.
        (await _pro1.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest(["Backend Developer", ".NET  Developer", ".net developer"], "İstanbul"), JsonOptions))
            .EnsureSuccessStatusCode();
        profile = await _pro1.GetFromJsonAsync<JobSourceProfileResponse>("/api/job-sources/profile", JsonOptions);
        profile!.Titles.ShouldBe(["Backend Developer", ".NET Developer"]);

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // pro1, pro2 and free all asked for ".NET Developer" / İstanbul: one shared query, plus pro1's new one.
        (await db.JobSourceQueries.CountAsync()).ShouldBe(2);

        (await _pro1.DeleteAsync("/api/job-sources/profile")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _pro1.GetAsync("/api/job-sources/profile")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_The_Account_Takes_The_Users_Rows_And_Leaves_The_Shared_Postings()
    {
        await SweepAsync();

        (await _pro2.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        })).EnsureSuccessStatusCode();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserJobSourceProfiles.AnyAsync(p => p.UserId == _pro2Id)).ShouldBeFalse();
        (await db.UserJobSourceDeliveries.AnyAsync(d => d.UserId == _pro2Id)).ShouldBeFalse();
        (await db.UserJobSourceRuns.AnyAsync(r => r.UserId == _pro2Id)).ShouldBeFalse();
        (await db.ProEntitlements.AnyAsync(e => e.UserId == _pro2Id)).ShouldBeFalse();
        (await db.JobSourcePostings.CountAsync()).ShouldBe(13);
        (await db.UserJobSourceDeliveries.CountAsync(d => d.UserId == _pro1Id)).ShouldBe(13);
    }

    private async Task SweepAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobSourceSweepService>().SweepAsync(CancellationToken.None);
    }

    private async Task<JobSourceDeliveriesResponse> ListAsync(HttpClient client, int? week = null)
    {
        var url = week is null ? "/api/job-sources/postings" : $"/api/job-sources/postings?week={week}";
        return (await client.GetFromJsonAsync<JobSourceDeliveriesResponse>(url, JsonOptions))!;
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Job", "Source", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        return (client, userId);
    }
}
