using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.CandidateExperiences;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CandidateExperiences;

public sealed class CandidateExperienceFlowProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // Small on purpose so the quota test does not have to write ten entries.
        builder.UseSetting("CandidateExperiences:MaxEntriesPerUser", "2");
        builder.UseSetting("CandidateExperiences:MinimumEntriesForStats", "3");
        builder.UseSetting("CandidateExperiences:PriorWeight", "5");
    }
}

/// <summary>
/// Candidate experiences end to end: reading is public, the reader's row carries a quarter but
/// never the author or the date, only the overall rating is required, one entry per company per
/// account, the total quota holds and deleting frees it, other people's rows are 404 to edit,
/// the aggregates appear only at the threshold, and the public company page counts entries.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CandidateExperienceFlowTests(ApiHost<CandidateExperienceFlowProfile> host)
    : IClassFixture<ApiHost<CandidateExperienceFlowProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email, WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? _factory).CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, (factory ?? _factory).Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Experience", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private static CandidateExperienceRequest Experience(int overall = 4,
        IReadOnlyList<ExperienceCategoryRatingDto>? categories = null, IReadOnlyList<string>? liked = null,
        IReadOnlyList<string>? improvable = null, HiringOutcome? outcome = HiringOutcome.Rejected,
        ProcessDuration? duration = ProcessDuration.TwoToFourWeeks, StageCount? stages = StageCount.Three,
        IReadOnlyList<InterviewType>? types = null) =>
        new(overall, categories ?? [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)],
            liked ?? ["communication.pos.steps_clear_upfront"], improvable ?? ["outcome.imp.notification"],
            outcome, duration, stages, types ?? [InterviewType.Video, InterviewType.TakeHomeAssignment]);

    private async Task<MyCandidateExperienceResponse> ShareAsync(HttpClient client, Guid companyId, CandidateExperienceRequest? request = null)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences", request ?? Experience(), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions))!;
    }

    private async Task<CandidateExperiencePageResponse> ListAsync(string slug) =>
        (await _factory.CreateClient().GetFromJsonAsync<CandidateExperiencePageResponse>($"/api/companies/public/{slug}/experiences", JsonOptions))!;

    // ---- Reading ------------------------------------------------------------------------------

    [Fact]
    public async Task Reading_Is_Public_But_Writing_Needs_An_Account()
    {
        var author = await RegisterAsync("read.experience@example.com");
        var company = await ResolveAsync(author, "Read Experience Co");
        await ShareAsync(author, company.Id);

        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync($"/api/companies/public/{company.Slug}/experiences")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.GetAsync($"/api/companies/{company.Id}/experiences/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync($"/api/companies/{company.Id}/experiences", Experience(), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/candidate-experiences/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Reader_Sees_A_Quarter_But_Never_The_Author_Or_The_Date()
    {
        var author = await RegisterAsync("quarter.author@example.com");
        var company = await ResolveAsync(author, "Quarter Co");
        await ShareAsync(author, company.Id);

        var raw = await _factory.CreateClient().GetStringAsync($"/api/companies/public/{company.Slug}/experiences");
        raw.ShouldNotContain("userId", Case.Insensitive);
        raw.ShouldNotContain("submittedAt", Case.Insensitive);
        raw.ShouldNotContain("submittedMonth", Case.Insensitive);
        raw.ShouldNotContain("occupation", Case.Insensitive);
        raw.ShouldNotContain("@example.com");

        var page = JsonSerializer.Deserialize<CandidateExperiencePageResponse>(raw, JsonOptions)!;
        var row = page.Items.ShouldHaveSingleItem();
        row.OverallRating.ShouldBe(4);
        row.CategoryRatings.ShouldHaveSingleItem().ShouldBe(new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5));
        row.LikedStatements.ShouldBe(["communication.pos.steps_clear_upfront"]);
        row.ImprovableStatements.ShouldBe(["outcome.imp.notification"]);
        row.Outcome.ShouldBe(HiringOutcome.Rejected);
        row.Duration.ShouldBe(ProcessDuration.TwoToFourWeeks);
        row.Stages.ShouldBe(StageCount.Three);
        row.InterviewTypes.ShouldBe([InterviewType.Video, InterviewType.TakeHomeAssignment]);
        var now = DateTimeOffset.UtcNow;
        row.SubmittedQuarter.ShouldBe($"{now.Year}-Q{(now.Month - 1) / 3 + 1}");
        page.Total.ShouldBe(1);
        page.PageSize.ShouldBe(10);
        page.Summary.Count.ShouldBe(1);
        page.Summary.MinimumForStats.ShouldBe(3);
    }

    [Fact]
    public async Task Unknown_Company_Is_Not_Found()
    {
        var author = await RegisterAsync("unknown.experience@example.com");

        (await _factory.CreateClient().GetAsync("/api/companies/public/no-such-company/experiences")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await author.GetAsync($"/api/companies/{Guid.NewGuid()}/experiences/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await author.PostAsJsonAsync($"/api/companies/{Guid.NewGuid()}/experiences", Experience(), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Writing rules ------------------------------------------------------------------------

    [Fact]
    public async Task An_Overall_Rating_Alone_Is_Enough_To_Save()
    {
        var author = await RegisterAsync("bare.experience@example.com");
        var company = await ResolveAsync(author, "Bare Co");

        var mine = await ShareAsync(author, company.Id, new CandidateExperienceRequest(3));

        mine.OverallRating.ShouldBe(3);
        mine.CategoryRatings.ShouldBeEmpty();
        mine.LikedStatements.ShouldBeEmpty();
        mine.Outcome.ShouldBeNull();
        mine.InterviewTypes.ShouldBeEmpty();
        mine.CompanySlug.ShouldBe(company.Slug);
        (await ListAsync(company.Slug)).Items.ShouldHaveSingleItem().OverallRating.ShouldBe(3);
    }

    [Fact]
    public async Task A_Second_Entry_For_The_Same_Company_Is_Refused()
    {
        var author = await RegisterAsync("twice.experience@example.com");
        var company = await ResolveAsync(author, "Twice Co");
        await ShareAsync(author, company.Id);

        var again = await author.PostAsJsonAsync($"/api/companies/{company.Id}/experiences", Experience(overall: 1), JsonOptions);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldContain("already rated your hiring process");
    }

    [Fact]
    public async Task Quota_Counts_Every_Company_And_Deleting_Frees_A_Slot()
    {
        var author = await RegisterAsync("quota.experience@example.com");
        var first = await ResolveAsync(author, "Quota One Co");
        var second = await ResolveAsync(author, "Quota Two Co");
        var third = await ResolveAsync(author, "Quota Three Co");
        var kept = await ShareAsync(author, first.Id);
        await ShareAsync(author, second.Id);

        var refused = await author.PostAsJsonAsync($"/api/companies/{third.Id}/experiences", Experience(), JsonOptions);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldContain("limit of 2 candidate experiences");

        (await author.DeleteAsync($"/api/candidate-experiences/{kept.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShareAsync(author, third.Id);

        var mine = await author.GetFromJsonAsync<MyCandidateExperiencesResponse>("/api/candidate-experiences/mine", JsonOptions);
        mine!.Items.Select(i => i.CompanyName).ShouldBe(["Quota Three Co", "Quota Two Co"]);
        mine.Quota.Used.ShouldBe(2);
        mine.Quota.Limit.ShouldBe(2);
        (await ListAsync(first.Slug)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Validation_Refuses_Unknown_Statements_And_Repeated_Interview_Types()
    {
        var author = await RegisterAsync("validation.experience@example.com");
        var company = await ResolveAsync(author, "Validation Co");

        var unknown = await author.PostAsJsonAsync($"/api/companies/{company.Id}/experiences",
            Experience(liked: ["communication.pos.made_up"]), JsonOptions);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await unknown.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions))!.Errors.Keys.ShouldContain(k => k.StartsWith("LikedStatements"));

        var repeated = await author.PostAsJsonAsync($"/api/companies/{company.Id}/experiences",
            Experience(types: [InterviewType.Panel, InterviewType.Panel]), JsonOptions);
        repeated.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await repeated.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions))!.Errors.Keys.ShouldContain("InterviewTypes");

        var offScale = await author.PostAsJsonAsync($"/api/companies/{company.Id}/experiences", Experience(overall: 0), JsonOptions);
        offScale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Ownership ----------------------------------------------------------------------------

    [Fact]
    public async Task Another_Persons_Entry_Is_Not_Found_To_Edit_Or_Delete()
    {
        var author = await RegisterAsync("owner.experience@example.com");
        var other = await RegisterAsync("other.experience@example.com");
        var company = await ResolveAsync(author, "Owner Co");
        var entry = await ShareAsync(author, company.Id);

        (await other.PutAsJsonAsync($"/api/candidate-experiences/{entry.Id}", Experience(overall: 1), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/candidate-experiences/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Untouched.
        (await ListAsync(company.Slug)).Items.ShouldHaveSingleItem().OverallRating.ShouldBe(4);
    }

    [Fact]
    public async Task Editing_Replaces_The_Ratings_Picks_And_Interview_Types()
    {
        var author = await RegisterAsync("edit.experience@example.com");
        var company = await ResolveAsync(author, "Edit Co");
        var entry = await ShareAsync(author, company.Id);

        var updated = await author.PutAsJsonAsync($"/api/candidate-experiences/{entry.Id}",
            Experience(overall: 2, categories: [new(ExperienceCategory.Punctuality, 1)], liked: [], improvable: ["punctuality.imp.start_time"],
                outcome: HiringOutcome.NoResponse, duration: null, stages: StageCount.One, types: [InterviewType.Phone]), JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = await updated.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions);
        row!.OverallRating.ShouldBe(2);
        row.CategoryRatings.ShouldHaveSingleItem().Category.ShouldBe(ExperienceCategory.Punctuality);
        row.LikedStatements.ShouldBeEmpty();
        row.ImprovableStatements.ShouldBe(["punctuality.imp.start_time"]);
        row.Outcome.ShouldBe(HiringOutcome.NoResponse);
        row.Duration.ShouldBeNull();
        row.Stages.ShouldBe(StageCount.One);
        row.InterviewTypes.ShouldBe([InterviewType.Phone]);

        var viewer = await author.GetFromJsonAsync<CandidateExperienceViewerStateResponse>($"/api/companies/{company.Id}/experiences/me", JsonOptions);
        viewer!.OwnEntry.ShouldNotBeNull().Id.ShouldBe(entry.Id);
        viewer.Quota.Used.ShouldBe(1);

        var publicRow = (await ListAsync(company.Slug)).Items.ShouldHaveSingleItem();
        publicRow.OverallRating.ShouldBe(2);
        publicRow.InterviewTypes.ShouldBe([InterviewType.Phone]);
    }

    // ---- Aggregates ---------------------------------------------------------------------------

    [Fact]
    public async Task Aggregates_Appear_Only_At_The_Threshold()
    {
        var first = await RegisterAsync("stats.one@example.com");
        var company = await ResolveAsync(first, "Stats Co");
        await ShareAsync(first, company.Id, Experience(overall: 4, outcome: HiringOutcome.Rejected, duration: ProcessDuration.TwoToFourWeeks));
        var second = await RegisterAsync("stats.two@example.com");
        await ShareAsync(second, company.Id, Experience(overall: 2, outcome: HiringOutcome.NoResponse, duration: ProcessDuration.OneToTwoMonths,
            categories: [new(ExperienceCategory.Communication, 1)], types: [InterviewType.Video]));

        var under = (await ListAsync(company.Slug)).Summary;
        under.Count.ShouldBe(2);
        under.AverageOverall.ShouldBe(3.0);
        under.Distribution.ShouldBe([0, 1, 0, 1, 0]);
        under.Score.ShouldBeNull();
        under.Categories.Single(c => c.Category == ExperienceCategory.Communication).Count.ShouldBe(2);
        under.Categories.Single(c => c.Category == ExperienceCategory.Communication).Average.ShouldBeNull();
        under.TopLiked.ShouldBeEmpty();
        under.Outcomes.ShouldBeEmpty();
        under.TypicalDuration.ShouldBeNull();
        under.InterviewTypes.ShouldBeEmpty();
        under.TakeHomeAssignmentCount.ShouldBe(0);

        var third = await RegisterAsync("stats.three@example.com");
        await ShareAsync(third, company.Id, Experience(overall: 5, outcome: HiringOutcome.Offer, duration: ProcessDuration.OneToTwoWeeks,
            categories: [new(ExperienceCategory.Communication, 3)], liked: ["communication.pos.steps_clear_upfront", "response.pos.quick_replies"],
            types: [InterviewType.Video, InterviewType.TakeHomeAssignment, InterviewType.Panel]));

        var at = (await ListAsync(company.Slug)).Summary;
        at.Count.ShouldBe(3);
        // Only these three entries exist, so the global average is their own mean: the score is
        // the plain average, 3.7.
        at.Score.ShouldBe(3.7);
        at.Categories.Single(c => c.Category == ExperienceCategory.Communication).Average.ShouldBe(3.0);
        at.TopLiked.First().ShouldBe(new ExperienceStatementCountResponse("communication.pos.steps_clear_upfront", 3));
        at.TopImprovable.ShouldHaveSingleItem().ShouldBe(new ExperienceStatementCountResponse("outcome.imp.notification", 3));
        at.Outcomes.Select(o => (o.Outcome, o.Count)).ShouldBe([(HiringOutcome.Offer, 1), (HiringOutcome.Rejected, 1), (HiringOutcome.NoResponse, 1)]);
        at.TypicalDuration.ShouldBe(ProcessDuration.TwoToFourWeeks);
        at.TypicalStages.ShouldBe(StageCount.Three);
        at.InterviewTypes.Select(t => (t.Type, t.Count)).ShouldBe([(InterviewType.Video, 3), (InterviewType.TakeHomeAssignment, 2), (InterviewType.Panel, 1)]);
        at.TakeHomeAssignmentCount.ShouldBe(2);
    }

    // ---- Public company page --------------------------------------------------------------------

    [Fact]
    public async Task The_Public_Company_Page_Counts_Entries()
    {
        var author = await RegisterAsync("count.experience@example.com");
        var company = await ResolveAsync(author, "Count Co");
        await ShareAsync(author, company.Id);

        var page = await _factory.CreateClient().GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        page!.CandidateExperienceCount.ShouldBe(1);
        page.SalaryCount.ShouldBe(0);
    }

    // ---- Rate limit -----------------------------------------------------------------------------

    [Fact]
    public async Task Writes_Have_Their_Own_Bucket()
    {
        // The suite disables rate limiting for every host; this test opts back in on a host of
        // its own (a limiter's fixed windows have no reset). Five writes are allowed an hour;
        // the sixth is a 429 — and a salary write, in its own bucket, still goes through.
        await using var limited = host.Standalone(builder => builder.UseSetting("RateLimiting:Enabled", "true"));
        var author = await RegisterAsync("limit.experience@example.com", limited);
        var company = await ResolveAsync(author, "Limit Co");
        var entry = await ShareAsync(author, company.Id);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await author.PutAsJsonAsync($"/api/candidate-experiences/{entry.Id}", Experience(overall: 1 + i % 5), JsonOptions);
        }

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await author.GetAsync($"/api/companies/{company.Id}/salaries/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
