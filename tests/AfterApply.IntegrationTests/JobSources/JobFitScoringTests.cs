using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.IntegrationTests.Identity;
using AfterApply.Domain.Ai;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Persistence;
using AfterApply.IntegrationTests.CvScan;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>What the class owns beyond the app: the LinkedIn transport, the model, the outbound
/// mail, and a scratch directory for CV storage. One of each serves the whole class.</summary>
public sealed class JobFitScoringProfile : IHostProfile
{
    public LinkedInStubHandler LinkedIn { get; } = new();
    public FakeScoringProvider Model { get; } = new();
    public CapturingEmailSender Email { get; } = new();
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "afterapply-job-fit-tests", Guid.CreateVersion7().ToString("N"));

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("JobSources:Enabled", "true");
        builder.UseSetting("JobSources:MinDelayMs", "0");
        builder.UseSetting("JobSources:RetryBaseDelayMs", "1");
        builder.UseSetting("JobSources:Scoring:ProjectId", "test-project");
        // LinkedIn alone: the source mix is JobSourceSweepTests' business.
        builder.UseSetting("JobSources:KariyerNetEnabled", "false");
        builder.UseSetting("Storage:LocalRootPath", StorageRoot);
        builder.UseSetting("App:WebBaseUrl", "https://ekariyerim.test");

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient(nameof(ILinkedInJobSourceClient)).ConfigurePrimaryHttpMessageHandler(() => LinkedIn);
            services.RemoveAll<IJobFitScoringProvider>();
            services.AddSingleton<IJobFitScoringProvider>(Model);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
        });
    }

    public void Reset()
    {
        LinkedIn.Reset();
        Model.Reset();
        Email.Reset();
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }

        return default;
    }
}

/// <summary>Scores by the number in the stub's title ("Software Developer 7" → 65), so the
/// expected order is known; throws for titles the test marks as failing; records every
/// request so the test can look at what the model was given.</summary>
public sealed class FakeScoringProvider : IJobFitScoringProvider
{
    public List<JobFitScoringRequest> Requests { get; } = [];

    public HashSet<string> FailingTitles { get; } = [];

    public string Model => "fake-model";

    public void Reset()
    {
        lock (Requests)
        {
            Requests.Clear();
        }

        FailingTitles.Clear();
    }

    public Task<JobFitScoringResult?> ScoreAsync(JobFitScoringRequest request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        if (FailingTitles.Contains(request.Title))
        {
            throw new JobFitScoringProviderException("Vertex AI returned 503.");
        }

        var n = int.Parse(request.Title.Split(' ')[^1]);
        return Task.FromResult<JobFitScoringResult?>(new JobFitScoringResult(100 - n * 5,
            request.Locale == "tr" ? $"İlan {n} için özet." : $"Summary for posting {n}.",
            ["C#", ".NET"], n % 2 == 0 ? ["Kubernetes"] : [], ["C#", ".NET", "Kubernetes"], 1_000, 100));
    }
}

/// <summary>
/// The scoring half of the weekly run, end to end with a stubbed LinkedIn and a fake model: a
/// real CV goes through storage and the extractor, every delivered posting gets one call, the
/// score orders the list and the threshold hides what is under it, the ceilings stop the spending,
/// and the ledger survives the account. The model is the one thing faked — everything from the
/// CV bytes to the API response is real.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobFitScoringTests(ApiHost<JobFitScoringProfile> host) : IClassFixture<ApiHost<JobFitScoringProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private static readonly DateTimeOffset RunMoment = DateTimeOffset.UtcNow;
    private static readonly int ThisWeek = WeekKey.From(RunMoment);

    // The class's host by default; the two tests that need different settings swap in a
    // Standalone host over a freshly reset database (see WithSettingsAsync).
    private WebApplicationFactory<Program> _factory = null!;
    private WebApplicationFactory<Program>? _standalone;
    private LinkedInStubHandler _linkedIn => host.Profile.LinkedIn;
    private FakeScoringProvider _model => host.Profile.Model;
    private CapturingEmailSender _email => host.Profile.Email;
    private HttpClient _admin = null!;
    private HttpClient _pro = null!;
    private Guid _proId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _factory = host;
        await ReRegisterAsync();
    }

    public async Task DisposeAsync()
    {
        if (_standalone is not null)
        {
            await _standalone.DisposeAsync();
        }
    }

    /// <summary>A host with different settings over the same, freshly reset database — the two
    /// users, the entitlement and the CV again on top.</summary>
    private async Task WithSettingsAsync(params (string Key, string Value)[] settings)
    {
        if (_standalone is not null)
        {
            await _standalone.DisposeAsync();
        }

        await host.ResetAsync();
        _standalone = host.Standalone(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });
        _factory = _standalone;
        await ReRegisterAsync();
    }

    [Fact]
    public async Task Every_Delivered_Posting_Is_Scored_Against_The_Real_Cv_And_The_List_Is_Ordered_By_Score()
    {
        await SaveProfileAsync();

        await SweepAsync();

        // 13 postings delivered, 13 calls, each carrying the CV's text as extracted from the PDF.
        _model.Requests.Count.ShouldBe(13);
        _model.Requests.ShouldAllBe(r => r.CvText.Contains("Ahmet Yilmaz") && r.Description.StartsWith("Description of posting") && r.Locale == "tr");
        _model.Requests.Select(r => r.Title).Distinct().Count().ShouldBe(13);

        var list = await ListAsync();
        list.Items.Count.ShouldBe(13);
        list.Items.Select(i => i.Score).ShouldBe(list.Items.Select(i => i.Score).OrderByDescending(s => s));
        list.Items[0].Title.ShouldBe("Software Developer 1");
        list.Items[0].Score.ShouldBe(95);
        list.Items[0].ScoreSummary.ShouldBe("İlan 1 için özet.");
        list.Run.ShouldNotBeNull();
        list.Run.ScoredCount.ShouldBe(13);
        list.Run.HiddenBelowMinScoreCount.ShouldBe(0);

        var detail = await _pro.GetFromJsonAsync<JobSourcePostingDetailResponse>($"/api/job-sources/postings/{list.Items[1].Id}", JsonOptions);
        detail!.Score.ShouldBe(90);
        detail.MatchedCriteria.ShouldBe(["C#", ".NET"]);
        detail.MissingCriteria.ShouldBe(["Kubernetes"]);
        detail.RequiredSkills.ShouldBe(["C#", ".NET", "Kubernetes"]);
        detail.ScoredAt.ShouldNotBeNull();

        // The ledger has one row per call with the tokens the provider reported, and the admin
        // view turns them into today's count and the month's estimated cost at list price.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AiUsageEntries.ToListAsync();
        entries.Count.ShouldBe(13);
        entries.ShouldAllBe(e => e.UserId == _proId && e.Feature == AiFeature.JobFitScoring && e.Model == "fake-model" && e.Succeeded);
        entries.Sum(e => e.InputTokens).ShouldBe(13_000);

        var usage = await _admin.GetFromJsonAsync<JobSourceUsageResponse>("/api/admin/job-sources/usage", JsonOptions);
        usage!.Scoring.CallsToday.ShouldBe(13);
        usage.Scoring.InputTokensThisMonth.ShouldBe(13_000);
        usage.Scoring.OutputTokensThisMonth.ShouldBe(1_300);
        // 13k × 0.30/M + 1.3k × 2.50/M = 0.0039 + 0.00325 = 0.00715 → 0.007
        usage.Scoring.EstimatedCostUsdThisMonth.ShouldBe(0.007m);

        // A second run in the same week has nothing left to score.
        await SweepAsync();
        _model.Requests.Count.ShouldBe(13);
    }

    [Fact]
    public async Task One_Digest_Goes_Out_Per_User_Per_Week_Saying_What_The_Page_Shows()
    {
        await SaveProfileAsync(minScore: 80);
        await SweepAsync();

        var (to, locale, digest) = await WaitForDigestAsync();
        to.ShouldBe("pro.jobfit@example.com");
        locale.ShouldBe("tr");
        // Four of thirteen score 80 or more — the count is the page's, not the sweep's.
        digest.Count.ShouldBe(4);
        digest.BestTitle.ShouldBe("Software Developer 1");
        digest.BestScore.ShouldBe(95);
        digest.Link.ShouldBe("https://ekariyerim.test/tr/weekly-jobs");

        // A second run in the same week queues nothing new.
        await SweepAsync();
        host.Jobs.Pending.ShouldBeEmpty();
        _email.Digests.Count.ShouldBe(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserJobSourceRuns.SingleAsync(r => r.UserId == _proId)).DigestSentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_Digest_Can_Be_Switched_Off_On_The_Profile()
    {
        (await _pro.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul", AcceptAiScoring: true, EmailDigest: false), JsonOptions))
            .EnsureSuccessStatusCode();
        (await _pro.GetFromJsonAsync<JobSourceProfileResponse>("/api/job-sources/profile", JsonOptions))!.EmailDigest.ShouldBeFalse();

        await SweepAsync();

        host.Jobs.Pending.ShouldBeEmpty();
        _email.Digests.ShouldBeEmpty();
        (await ListAsync()).Items.Count.ShouldBe(13);
    }

    // The digest goes out as its own background job per user (see JobSourceDigestService); the
    // sweep enqueued it, this runs it.
    private async Task<(string ToEmail, string Locale, WeeklyJobsDigest Digest)> WaitForDigestAsync()
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();
        lock (_email.Digests)
        {
            _email.Digests.ShouldNotBeEmpty("the weekly jobs digest job did not send");
            return _email.Digests[0];
        }
    }

    [Fact]
    public async Task The_Threshold_Hides_Low_Scores_At_Read_Time_And_Can_Be_Moved()
    {
        await SaveProfileAsync(minScore: 80);
        await SweepAsync();

        // Scores are 95, 90, ..., 35: four are 80 or more.
        var list = await ListAsync();
        list.Items.Count.ShouldBe(4);
        list.Items.ShouldAllBe(i => i.Score >= 80);
        list.Run!.ScoredCount.ShouldBe(13);
        list.Run.HiddenBelowMinScoreCount.ShouldBe(9);

        // Lowering it re-reads the same rows — no fetch, no call.
        await SaveProfileAsync(minScore: 50);
        list = await ListAsync();
        list.Items.Count.ShouldBe(10);
        list.Run!.HiddenBelowMinScoreCount.ShouldBe(3);
        _model.Requests.Count.ShouldBe(13);
    }

    [Fact]
    public async Task A_Profile_Cannot_Be_Created_Without_Consent_And_Nothing_Is_Scored_Without_It()
    {
        var refused = await _pro.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul"), JsonOptions);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("rıza");

        // A profile that exists without the stamp (the row predates the rule, say) is swept for
        // but never scored: the CVs page's promise holds for anyone who has not said yes.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profileService = scope.ServiceProvider.GetRequiredService<IUserJobSourceProfileService>();
            await profileService.UpsertAsync(_proId, new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul", AcceptAiScoring: true),
                CancellationToken.None);
            await db.UserJobSourceProfiles.Where(p => p.UserId == _proId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.AiScoringConsentAcceptedAt, (DateTimeOffset?)null));
        }

        await SweepAsync();

        _model.Requests.ShouldBeEmpty();
        var list = await ListAsync();
        list.Items.Count.ShouldBe(13);
        list.Items.ShouldAllBe(i => i.Score == null);
        list.Run!.ScoredCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_Per_User_Weekly_Cap_Scores_The_Best_Ranked_First_And_Holds_Across_Runs()
    {
        await WithSettingsAsync(("JobSources:Scoring:MaxPerUserPerWeek", "4"));
        await SaveProfileAsync();

        await SweepAsync();
        await SweepAsync();

        _model.Requests.Count.ShouldBe(4);
        _model.Requests.Select(r => r.Title).ShouldBe(["Software Developer 1", "Software Developer 2", "Software Developer 3", "Software Developer 4"]);
        var list = await ListAsync();
        list.Items.Count.ShouldBe(13);
        list.Items.Take(4).ShouldAllBe(i => i.Score != null);
        list.Items.Skip(4).ShouldAllBe(i => i.Score == null);
    }

    [Fact]
    public async Task The_Daily_Call_Ceiling_Stops_The_Run_And_Scoring_Can_Be_Turned_Off()
    {
        await WithSettingsAsync(("JobSources:Scoring:MaxCallsPerDay", "5"));
        await SaveProfileAsync();

        await SweepAsync();
        _model.Requests.Count.ShouldBe(5);

        await WithSettingsAsync(("JobSources:Scoring:Enabled", "false"));
        await SaveProfileAsync();

        await SweepAsync();
        _model.Requests.ShouldBeEmpty();
        (await ListAsync()).Items.Count.ShouldBe(13);
    }

    [Fact]
    public async Task A_Failed_Call_Is_Retried_Once_Next_Run_Then_Given_Up_On_And_Still_Counted()
    {
        _model.FailingTitles.Add("Software Developer 3");
        await SaveProfileAsync();

        await SweepAsync();
        _model.Requests.Count.ShouldBe(13);
        var list = await ListAsync();
        list.Run!.ScoredCount.ShouldBe(12);
        list.Items.Single(i => i.Title == "Software Developer 3").Score.ShouldBeNull();

        // Second run: only the failed one is tried again, and it fails again.
        await SweepAsync();
        _model.Requests.Count.ShouldBe(14);
        _model.Requests[^1].Title.ShouldBe("Software Developer 3");

        // Third run: attempts exhausted, nothing is bought.
        await SweepAsync();
        _model.Requests.Count.ShouldBe(14);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AiUsageEntries.CountAsync(e => !e.Succeeded)).ShouldBe(2);
        (await db.UserJobSourceDeliveries.Where(d => d.UserId == _proId && d.Score == null).Select(d => d.ScoreAttempts).SingleAsync())
            .ShouldBe(UserJobSourceDelivery.MaxScoreAttempts);
    }

    [Fact]
    public async Task The_Ledger_Outlives_The_Account_With_The_User_Cleared()
    {
        await SaveProfileAsync();
        await SweepAsync();

        (await _pro.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        })).EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(u => u.Id == _proId)).ShouldBeFalse();
        (await db.UserJobSourceDeliveries.AnyAsync(d => d.UserId == _proId)).ShouldBeFalse();
        var entries = await db.AiUsageEntries.ToListAsync();
        entries.Count.ShouldBe(13);
        entries.ShouldAllBe(e => e.UserId == null);
    }

    private async Task SaveProfileAsync(int minScore = 0) =>
        (await _pro.PutAsJsonAsync("/api/job-sources/profile",
            new UpsertJobSourceProfileRequest([".NET Developer"], "İstanbul", MinScore: minScore, AcceptAiScoring: true), JsonOptions))
        .EnsureSuccessStatusCode();

    private async Task SweepAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IJobSourceSweepService>().SweepAsync(CancellationToken.None);
    }

    private async Task<JobSourceDeliveriesResponse> ListAsync() =>
        (await _pro.GetFromJsonAsync<JobSourceDeliveriesResponse>($"/api/job-sources/postings?week={ThisWeek}", JsonOptions))!;

    /// <summary>The two users, the entitlement and the CV — on whichever host _factory is.</summary>
    private async Task ReRegisterAsync()
    {
        (_admin, _) = await RegisterAsync("admin.jobfit@ekariyerim.com");
        (_pro, _proId) = await RegisterAsync("pro.jobfit@example.com");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "admin.jobfit@ekariyerim.com");
            admin.IsAdmin = true;
            await db.SaveChangesAsync();
        }

        (await _admin.PutAsJsonAsync($"/api/admin/pro/entitlements/{_proId}",
            new GrantProEntitlementRequest(RunMoment.AddMonths(1)), JsonOptions)).EnsureSuccessStatusCode();
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(CvFixtures.ReadablePdf()), "file", "cv.pdf" },
            { new StringContent("true"), "consentAccepted" }
        };
        (await _pro.PostAsync("/api/cv-documents", content)).EnsureSuccessStatusCode();
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Job", "Fit", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        return (client, userId);
    }
}
