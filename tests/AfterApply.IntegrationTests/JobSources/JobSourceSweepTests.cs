using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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
/// <summary>Both job sources stubbed, and a clock the tests move. One of each for the class.</summary>
public sealed class JobSourceSweepProfile : IHostProfile
{
    public LinkedInStubHandler LinkedIn { get; } = new();
    public KariyerNetStubHandler KariyerNet { get; } = new();
    public MutableTimeProvider Clock { get; } = new(JobSourceSweepTests.RunMoment);

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("JobSources:Enabled", "true");
        builder.UseSetting("JobSources:MinDelayMs", "0");
        builder.UseSetting("JobSources:RetryBaseDelayMs", "1");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient(nameof(ILinkedInJobSourceClient))
                .ConfigurePrimaryHttpMessageHandler(() => LinkedIn);
            services.AddHttpClient(nameof(IKariyerNetJobSourceClient))
                .ConfigurePrimaryHttpMessageHandler(() => KariyerNet);
        });
    }

    public void Reset()
    {
        LinkedIn.Reset();
        KariyerNet.Reset();
        Clock.Reset();
    }
}

[Collection(IntegrationTestCollection.Name)]
public class JobSourceSweepTests(ApiHost<JobSourceSweepProfile> host) : IClassFixture<ApiHost<JobSourceSweepProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    // The clock under test starts at the real "now" and only ever moves forward: access tokens
    // carry a not-before stamped from the same TimeProvider, and the JWT middleware checks them
    // against the wall clock — a fake clock in the past or a login after an advance would not
    // authenticate.
    internal static readonly DateTimeOffset RunMoment = DateTimeOffset.UtcNow;
    private static readonly int ThisWeek = WeekKey.From(RunMoment);
    private static readonly int NextWeek = WeekKey.From(RunMoment.AddDays(7));

    private WebApplicationFactory<Program> _factory => host;
    private LinkedInStubHandler _linkedIn => host.Profile.LinkedIn;
    private KariyerNetStubHandler _kariyer => host.Profile.KariyerNet;
    private MutableTimeProvider _clock => host.Profile.Clock;
    private HttpClient _admin = null!;
    private HttpClient _pro1 = null!;
    private HttpClient _pro2 = null!;
    private HttpClient _free = null!;
    private Guid _pro1Id;
    private Guid _pro2Id;
    private Guid _freeId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

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
                new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul", AcceptAiScoring: true), JsonOptions)).EnsureSuccessStatusCode();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Shared_Query_Is_Fetched_Once_And_Both_Paying_Users_Get_The_Postings()
    {
        await SweepAsync();

        // Two users, one title, two sources: LinkedIn's query is two pages (10 + 3 cards) fetched
        // once for both users; kariyer.net's is one page (4 cards), likewise once.
        _linkedIn.SearchRequests.ShouldBe(2);
        _kariyer.SearchRequests.ShouldBe(1);
        // Every delivered posting got its description, once — 13 + 4 distinct postings.
        _linkedIn.PostingRequests.ShouldBe(13);
        _kariyer.PostingRequests.ShouldBe(4);

        var pro1 = await ListAsync(_pro1);
        var pro2 = await ListAsync(_pro2);
        pro1.Items.Count.ShouldBe(17);
        pro2.Items.Count.ShouldBe(17);
        pro1.Run.ShouldNotBeNull();
        pro1.Run.WeekKey.ShouldBe(ThisWeek);
        pro1.Run.CandidateCount.ShouldBe(17);
        pro1.Run.DeliveredCount.ShouldBe(17);
        pro1.Run.ExcludedAppliedCount.ShouldBe(0);
        pro1.Items.Count(i => i.Source == Source.LinkedIn).ShouldBe(13);
        pro1.Items.Count(i => i.Source == Source.KariyerNet).ShouldBe(4);

        // Round-robin across the two sources' lanes: the best of each alternates at the top.
        pro1.Items[0].Title.ShouldBe("Software Developer 1");
        pro1.Items[1].Title.ShouldBe("Kariyer .NET Geliştirici 1");
        pro1.Items[0].Seniority.ShouldBe("Mid-Senior level");
        var detail = await _pro1.GetFromJsonAsync<JobSourcePostingDetailResponse>($"/api/job-sources/postings/{pro1.Items[0].Id}", JsonOptions);
        detail!.Description.ShouldBe("Description of posting 4460000001.\nC#\n.NET");
        detail.Url.ShouldBe("https://www.linkedin.com/jobs/view/4460000001");

        var kariyer = await _pro1.GetFromJsonAsync<JobSourcePostingDetailResponse>($"/api/job-sources/postings/{pro1.Items[1].Id}", JsonOptions);
        kariyer!.Source.ShouldBe(Source.KariyerNet);
        kariyer.Url.ShouldBe("https://www.kariyer.net/is-ilani/firma-1-net-gelistirici-4550000001");
        kariyer.Description.ShouldBe("kariyer.net ilanı 4550000001 açıklaması.\nC#\n.NET");
        kariyer.Seniority.ShouldBe("En az 3 yıl tecrübeli");
        kariyer.Location.ShouldBe("İstanbul");
        kariyer.PostedAt.ShouldBe(DateOnly.FromDateTime(RunMoment.UtcDateTime).AddDays(-1));
    }

    [Fact]
    public async Task A_Kariyer_Net_Stop_Leaves_LinkedIn_Running_And_Vice_Versa()
    {
        // kariyer.net answers 429 to its first search; LinkedIn is unaffected, and kariyer.net's
        // own cooldown is what the admin view reports.
        _kariyer.AnswerNextWith(HttpStatusCode.TooManyRequests);
        await SweepAsync();

        var pro1 = await ListAsync(_pro1);
        pro1.Items.Count.ShouldBe(13);
        pro1.Items.ShouldAllBe(i => i.Source == Source.LinkedIn);

        var usage = await _admin.GetFromJsonAsync<JobSourceUsageResponse>("/api/admin/job-sources/usage", JsonOptions);
        usage!.Sources.Single(s => s.Source == Source.KariyerNet).CooldownUntil.ShouldNotBeNull();
        usage.Sources.Single(s => s.Source == Source.LinkedIn).CooldownUntil.ShouldBeNull();
        usage.Sources.Single(s => s.Source == Source.LinkedIn).RequestsToday.ShouldBe(15);
        usage.Sources.Single(s => s.Source == Source.KariyerNet).RequestsToday.ShouldBe(1);

        // Next day kariyer.net is tried again and its postings join the week — unless the next
        // day is a new week (a Sunday run; see MutableTimeProvider.StaysInIsoWeek).
        if (!_clock.StaysInIsoWeek(TimeSpan.FromHours(25))) return;
        _clock.Advance(TimeSpan.FromHours(25));
        await SweepAsync();
        (await ListAsync(_pro1)).Items.Count.ShouldBe(17);
    }

    [Fact]
    public async Task Users_Who_Are_Not_Paying_Or_Have_No_Cv_Or_Are_Inactive_Are_Not_Swept()
    {
        // pro2 loses the CV; pro1 has not signed in for a month; free never paid.
        await using (var scope = _factory.Services.CreateAsyncScope())
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
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.FirstAsync();
            db.Applications.Add(DomainApplication.Create(_pro1Id, company.Id, "Software Developer 3",
                "https://tr.linkedin.com/jobs/view/software-developer-3-at-acme-4460000003", "İstanbul",
                EmploymentType.FullTime, RunMoment, Source.Manual, null, RunMoment));
            await db.SaveChangesAsync();
        }

        // ...and a kariyer.net one, by its canonical URL.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.FirstAsync();
            db.Applications.Add(DomainApplication.Create(_pro1Id, company.Id, "Kariyer .NET Geliştirici 3",
                "https://www.kariyer.net/is-ilani/firma-3-net-gelistirici-4550000003", "İstanbul",
                EmploymentType.FullTime, RunMoment, Source.Manual, null, RunMoment));
            await db.SaveChangesAsync();
        }

        await SweepAsync();

        var pro1 = await ListAsync(_pro1);
        pro1.Items.Count.ShouldBe(14);
        pro1.Items.Select(i => i.Url).ShouldNotContain("https://www.linkedin.com/jobs/view/4460000001");
        pro1.Items.Select(i => i.Url).ShouldNotContain("https://www.linkedin.com/jobs/view/4460000003");
        pro1.Items.Select(i => i.Url).ShouldNotContain("https://www.kariyer.net/is-ilani/firma-3-net-gelistirici-4550000003");
        pro1.Run!.CandidateCount.ShouldBe(17);
        pro1.Run.DeliveredCount.ShouldBe(14);
        pro1.Run.ExcludedAppliedCount.ShouldBe(3);
        pro1.Run.ExcludedRecentlyShownCount.ShouldBe(0);

        // The other user did not apply: the same postings reach them.
        var pro2 = await ListAsync(_pro2);
        pro2.Items.Count.ShouldBe(17);
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

        (await ListAsync(_pro1)).Items.Count.ShouldBe(17);
        var pro2 = await ListAsync(_pro2);
        pro2.Items.Count.ShouldBe(5);
        pro2.Run!.DeliveredCount.ShouldBe(5);
        pro2.Run.CandidateCount.ShouldBe(17);
        // Only what was delivered gets a description: pro1's 13 + 4 cover pro2's 5.
        _linkedIn.PostingRequests.ShouldBe(13);
        _kariyer.PostingRequests.ShouldBe(4);

        // The same week again — a late or repeated job: nothing new, and neither site is asked again.
        // (An hour, not days: the clock started at the real "now", which may be a Sunday.)
        _clock.Advance(TimeSpan.FromHours(1));
        await SweepAsync();

        _linkedIn.SearchRequests.ShouldBe(2);
        _kariyer.SearchRequests.ShouldBe(1);
        (await ListAsync(_pro2)).Items.Count.ShouldBe(5);
        (await ListAsync(_pro1)).Run!.DeliveredCount.ShouldBe(17);
    }

    [Fact]
    public async Task Next_Week_The_Query_Runs_Again_And_Only_New_Postings_Are_Delivered()
    {
        await SweepAsync();
        (await ListAsync(_pro1)).Items.Count.ShouldBe(17);

        _clock.Advance(TimeSpan.FromDays(7));
        _linkedIn.Cards.Insert(0, ("4460000099", "Brand New Posting", "Kuzey Teknoloji", "İstanbul", "2026-09-20"));
        await SweepAsync();

        _linkedIn.SearchRequests.ShouldBe(4);
        _kariyer.SearchRequests.ShouldBe(2);
        var thisWeek = await ListAsync(_pro1);
        thisWeek.Run!.WeekKey.ShouldBe(NextWeek);
        thisWeek.Items.Select(i => i.Title).ShouldBe(["Brand New Posting"]);
        thisWeek.Run.CandidateCount.ShouldBe(18);
        thisWeek.Run.DeliveredCount.ShouldBe(1);
        thisWeek.Run.ExcludedRecentlyShownCount.ShouldBe(17);

        // Last week is still readable by its key.
        var lastWeek = await ListAsync(_pro1, ThisWeek);
        lastWeek.Items.Count.ShouldBe(17);
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
    public async Task I_Applied_Records_An_Application_Once_And_Only_For_A_Delivered_Posting()
    {
        await SweepAsync();
        var pro1 = await ListAsync(_pro1);
        var posting = pro1.Items[0];

        var first = await _pro1.PostAsync($"/api/job-sources/postings/{posting.Id}/apply", null);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var application = await first.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        application!.JobTitle.ShouldBe(posting.Title);
        application.CompanyName.ShouldBe(posting.CompanyName);
        application.JobUrl.ShouldBe(posting.Url);
        application.Source.ShouldBe(Source.LinkedIn);

        // Pressed again: the same application, not a second one.
        var second = await _pro1.PostAsync($"/api/job-sources/postings/{posting.Id}/apply", null);
        (await second.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Id.ShouldBe(application.Id);

        // Somebody else's posting id opens nothing, and the free user has no deliveries at all.
        (await _free.PostAsync($"/api/job-sources/postings/{posting.Id}/apply", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Next week's run treats it as applied: left out and counted.
        _clock.Advance(TimeSpan.FromDays(7));
        await SweepAsync();
        var nextWeek = await ListAsync(_pro1);
        nextWeek.Run!.ExcludedAppliedCount.ShouldBe(1);
        nextWeek.Items.ShouldNotContain(i => i.Id == posting.Id);
    }

    [Fact]
    public async Task Status_Says_Who_Is_Paying_And_Whether_There_Is_A_Cv_And_Criteria()
    {
        var pro = await _pro1.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions);
        pro!.IsPro.ShouldBeTrue();
        pro.ProActiveUntil.ShouldNotBeNull();
        pro.HasCv.ShouldBeTrue();
        pro.CvFileName.ShouldBe("cv.pdf");
        pro.HasProfile.ShouldBeTrue();

        var free = await _free.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions);
        free!.IsPro.ShouldBeFalse();
        free.ProActiveUntil.ShouldBeNull();

        (await _free.DeleteAsync("/api/job-sources/profile")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _free.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions))!.HasProfile.ShouldBeFalse();

        // The public config says the feature is on, so the web app shows the page at all.
        var config = await _free.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/config", JsonOptions);
        config.GetProperty("jobSources").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task The_Dashboard_Announcement_Stays_Closed_Once_Dismissed_For_That_Account_Only()
    {
        (await _free.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions))!.AnnouncementDismissed.ShouldBeFalse();

        (await _free.PostAsync("/api/job-sources/announcement/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Idempotent: a second close is a no-op, not an error.
        (await _free.PostAsync("/api/job-sources/announcement/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _free.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions))!.AnnouncementDismissed.ShouldBeTrue();
        // Per account: another user's card is untouched.
        (await _pro1.GetFromJsonAsync<JobSourceStatusResponse>("/api/job-sources/status", JsonOptions))!.AnnouncementDismissed.ShouldBeFalse();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.SingleAsync(u => u.Id == _freeId)).WeeklyJobsAnnouncementDismissedAt.ShouldNotBeNull();
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
        // shared query behind ".NET Developer" is reused, and a duplicate spelling collapses. The
        // consent given at creation is not asked for again and stays recorded.
        (await _pro1.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest(["Backend Developer", ".NET  Developer", ".net developer"], "İstanbul"), JsonOptions))
            .EnsureSuccessStatusCode();
        profile = await _pro1.GetFromJsonAsync<JobSourceProfileResponse>("/api/job-sources/profile", JsonOptions);
        profile!.Titles.ShouldBe(["Backend Developer", ".NET Developer"]);
        profile.AiScoringConsentAcceptedAt.ShouldNotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // pro1, pro2 and free all asked for ".NET Developer" / İstanbul: one shared query per
        // source, plus pro1's new title on each source.
        (await db.JobSourceQueries.CountAsync()).ShouldBe(4);
        (await db.JobSourceQueries.CountAsync(q => q.Source == Source.KariyerNet)).ShouldBe(2);

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

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserJobSourceProfiles.AnyAsync(p => p.UserId == _pro2Id)).ShouldBeFalse();
        (await db.UserJobSourceDeliveries.AnyAsync(d => d.UserId == _pro2Id)).ShouldBeFalse();
        (await db.UserJobSourceRuns.AnyAsync(r => r.UserId == _pro2Id)).ShouldBeFalse();
        (await db.ProEntitlements.AnyAsync(e => e.UserId == _pro2Id)).ShouldBeFalse();
        (await db.JobSourcePostings.CountAsync()).ShouldBe(17);
        (await db.UserJobSourceDeliveries.CountAsync(d => d.UserId == _pro1Id)).ShouldBe(17);
    }

    private async Task SweepAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobSourceSweepService>().SweepAsync(CancellationToken.None);
    }

    private async Task<JobSourceDeliveriesResponse> ListAsync(HttpClient client, int? week = null)
    {
        var url = week is null ? "/api/job-sources/postings" : $"/api/job-sources/postings?week={week}";
        return (await client.GetFromJsonAsync<JobSourceDeliveriesResponse>(url, JsonOptions))!;
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
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
