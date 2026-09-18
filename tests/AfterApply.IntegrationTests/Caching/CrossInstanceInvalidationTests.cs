using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Caching;

/// <summary>
/// The bug of 2026-09-18, pinned: production runs several Cloud Run instances, each with its own
/// L1, and a write on one of them must be visible on the next request to any other. Two hosts
/// here share the class's Postgres and Redis (the fixture and a Variant) but have separate
/// IMemoryCaches — exactly the multi-instance shape. Each test first reads through host A, so A's
/// L1 holds the entry, then writes through host B, then reads through A again and expects the
/// new value. Before the backplane, A served its stale L1 entry until the TTL lapsed.
///
/// A read that populates A's L1 first is what makes these tests mean something: without it they
/// would pass with no invalidation at all.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CrossInstanceInvalidationTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _a => host;
    private WebApplicationFactory<Program> _b => host.Variant("second-instance", _ => { });

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- The reported bug: a candidate experience ---------------------------------------------

    [Fact]
    public async Task An_Experience_Written_On_One_Instance_Changes_The_Summary_Served_By_Another()
    {
        var (authorOnB, _) = await host.RegisterAsync("experience.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Experience Co");

        // A caches the empty page (items, total and summary) before B writes.
        var before = await ExperiencesAsync(_a, company.Slug);
        before.Total.ShouldBe(0);
        before.Summary.Count.ShouldBe(0);

        await ShareExperienceAsync(authorOnB, company.Id);

        var after = await ExperiencesAsync(_a, company.Slug);
        after.Total.ShouldBe(1);
        after.Summary.Count.ShouldBe(1);
        after.Items.Count.ShouldBe(1);

        // A second round, because the first can pass without a backplane: A had never seen the
        // company's tag, so it read the tag's invalidation marker from L2 and rebuilt. Now A holds
        // that marker in its own L1, and only the backplane can tell it the tag moved again.
        var (secondAuthorOnB, _) = await host.RegisterAsync("experience.b2@example.com", on: _b);
        await ShareExperienceAsync(secondAuthorOnB, company.Id);

        var afterSecond = await ExperiencesAsync(_a, company.Slug);
        afterSecond.Total.ShouldBe(2);
        afterSecond.Summary.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_Experience_Deleted_On_One_Instance_Leaves_The_Public_Page_Served_By_Another()
    {
        var (authorOnB, _) = await host.RegisterAsync("experience.delete.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Deleted Experience Co");
        var mine = await ShareExperienceAsync(authorOnB, company.Id);

        (await PublicPageAsync(_a, company.Slug)).CandidateExperienceCount.ShouldBe(1);

        (await authorOnB.DeleteAsync($"/api/candidate-experiences/{mine.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PublicPageAsync(_a, company.Slug)).CandidateExperienceCount.ShouldBe(0);
    }

    // ---- Reviews: the page, the list, the helpful count --------------------------------------

    [Fact]
    public async Task A_Review_Written_On_One_Instance_Reaches_The_Page_And_The_List_On_Another()
    {
        var (authorOnB, _) = await host.RegisterAsync("review.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Review Co");

        (await PublicPageAsync(_a, company.Slug)).Summary.ApprovedCount.ShouldBe(0);
        (await ReviewsAsync(_a, company.Slug)).TotalCount.ShouldBe(0);

        await WriteReviewAsync(authorOnB, company.Id);

        (await PublicPageAsync(_a, company.Slug)).Summary.ApprovedCount.ShouldBe(1);
        (await ReviewsAsync(_a, company.Slug)).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_Helpful_Mark_On_One_Instance_Changes_The_Count_In_The_List_On_Another()
    {
        var (author, _) = await host.RegisterAsync("review.author@example.com");
        var (readerOnB, _) = await host.RegisterAsync("review.reader.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(author, "Cross Instance Helpful Co");
        var reviewId = await WriteReviewAsync(author, company.Id);

        (await ReviewsAsync(_a, company.Slug)).Items.Single().HelpfulCount.ShouldBe(0);

        var marked = await readerOnB.PostAsync($"/api/company-reviews/{reviewId}/helpful", null);
        marked.EnsureSuccessStatusCode();

        (await ReviewsAsync(_a, company.Slug)).Items.Single().HelpfulCount.ShouldBe(1);
    }

    // ---- Salaries -----------------------------------------------------------------------------

    [Fact]
    public async Task A_Salary_Shared_On_One_Instance_Appears_In_The_List_On_Another()
    {
        var (readerOnA, _) = await host.RegisterAsync("salary.reader.a@example.com");
        var (authorOnB, _) = await host.RegisterAsync("salary.author.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Salary Co");

        (await SalariesAsync(readerOnA, company.Id)).Total.ShouldBe(0);

        var shared = await authorOnB.PostAsJsonAsync($"/api/companies/{company.Id}/salaries",
            new CompanySalaryRequest(Occupation.IdFor("2512"), 6, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee,
                95_000m, SalaryCurrency.TRY, false), JsonOptions);
        shared.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await SalariesAsync(readerOnA, company.Id)).Total.ShouldBe(1);
        (await PublicPageAsync(_a, company.Slug)).SalaryCount.ShouldBe(1);
    }

    // ---- The directory ------------------------------------------------------------------------

    [Fact]
    public async Task A_First_Contribution_On_One_Instance_Adds_The_Company_To_The_Directory_On_Another()
    {
        var (authorOnB, _) = await host.RegisterAsync("directory.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Directory Co");

        (await DirectoryAsync(_a)).Items.ShouldNotContain(item => item.Id == company.Id);

        await ShareExperienceAsync(authorOnB, company.Id);

        (await DirectoryAsync(_a)).Items.ShouldContain(item => item.Id == company.Id);
    }

    // ---- Per-user entries ---------------------------------------------------------------------

    [Fact]
    public async Task Application_Counts_Written_On_One_Instance_Are_Read_Fresh_On_Another()
    {
        var (userOnA, auth) = await host.RegisterAsync("counts.a@example.com");
        using var userOnB = _b.CreateClient();
        userOnB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await SummaryAsync(userOnA)).Total.ShouldBe(0);

        var created = await userOnB.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Cross Instance Counts Co", "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        created.EnsureSuccessStatusCode();

        (await SummaryAsync(userOnA)).Total.ShouldBe(1);
    }

    [Fact]
    public async Task A_Token_Revoked_On_One_Instance_Stops_Authenticating_On_Another()
    {
        var (owner, _) = await host.RegisterAsync("pat.owner@example.com");
        var created = await owner.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Scripting", PersonalAccessTokenScope.Full), JsonOptions);
        created.EnsureSuccessStatusCode();
        var token = (await created.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions))!;

        // B validates the token and caches the result; the revoke lands on A.
        using var tokenOnB = _b.CreateClient();
        tokenOnB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        (await tokenOnB.GetAsync("/api/applications")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await owner.DeleteAsync($"/api/personal-access-tokens/{token.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await tokenOnB.GetAsync("/api/applications")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Company_Created_On_One_Instance_Replaces_The_Cached_Miss_On_Another()
    {
        // CompanyResolver caches "no such company" for ten minutes. A resolves (and caches null,
        // then the id it created); B must see that id, not its own null, or it would insert a
        // duplicate and hit the unique index.
        var (userOnA, auth) = await host.RegisterAsync("resolver.a@example.com");
        using var userOnB = _b.CreateClient();
        userOnB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var first = await CreateApplicationAsync(userOnA, "Cross Instance Resolver Co", "Engineer");
        var second = await CreateApplicationAsync(userOnB, "Cross Instance Resolver Co", "Senior Engineer");

        first.CompanyId.ShouldBe(second.CompanyId);
    }

    [Fact]
    public async Task Deleting_An_Account_On_One_Instance_Removes_Its_Contributions_From_Pages_On_Another()
    {
        var (authorOnB, _) = await host.RegisterAsync("deleted.author.b@example.com", on: _b);
        var company = await ResolveCompanyAsync(authorOnB, "Cross Instance Deleted Account Co");
        await ShareExperienceAsync(authorOnB, company.Id);
        await WriteReviewAsync(authorOnB, company.Id);

        var page = await PublicPageAsync(_a, company.Slug);
        page.CandidateExperienceCount.ShouldBe(1);
        page.Summary.ApprovedCount.ShouldBe(1);

        var deleted = await authorOnB.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiHost.DefaultPassword), options: JsonOptions)
        });
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        page = await PublicPageAsync(_a, company.Slug);
        page.CandidateExperienceCount.ShouldBe(0);
        page.Summary.ApprovedCount.ShouldBe(0);
    }

    /// <summary>The two hosts really are two L1s: what A cached is not in B's memory cache. If
    /// they ever shared one, every test above would pass without a backplane.</summary>
    [Fact]
    public void The_Two_Hosts_Have_Separate_Memory_Caches()
    {
        ReferenceEquals(
                _a.Services.GetRequiredService<IMemoryCache>(),
                _b.Services.GetRequiredService<IMemoryCache>())
            .ShouldBeFalse();
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private async Task<ResolvedCompanyResponse> ResolveCompanyAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private static async Task<MyCandidateExperienceResponse> ShareExperienceAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences",
            new CandidateExperienceRequest(4, Outcome: HiringOutcome.Rejected), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions))!;
    }

    private static async Task<Guid> WriteReviewAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", new CreateCompanyReviewRequest(
            EmploymentStatus.FormerEmployee, 4,
            [new ReviewCategoryRatingDto(ReviewCategory.Management, 3)],
            ["environment.pos.team_communication"],
            ["career.imp.promotion_transparency"]), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!.Id;
    }

    private static async Task<ApplicationDetailResponse> CreateApplicationAsync(HttpClient client, string companyName, string jobTitle)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, jobTitle, null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    private static Task<CandidateExperiencePageResponse> ExperiencesAsync(WebApplicationFactory<Program> instance, string slug) =>
        instance.CreateClient().GetFromJsonAsync<CandidateExperiencePageResponse>($"/api/companies/public/{slug}/experiences", JsonOptions)!;

    private static Task<CompanyPublicResponse> PublicPageAsync(WebApplicationFactory<Program> instance, string slug) =>
        instance.CreateClient().GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{slug}", JsonOptions)!;

    private static Task<PagedResult<CompanyReviewPublicResponse>> ReviewsAsync(WebApplicationFactory<Program> instance, string slug) =>
        instance.CreateClient().GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>($"/api/companies/public/{slug}/reviews", JsonOptions)!;

    private static Task<PagedResult<CompanyPublicListItemResponse>> DirectoryAsync(WebApplicationFactory<Program> instance) =>
        instance.CreateClient().GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>("/api/companies/public/", JsonOptions)!;

    private static Task<CompanySalaryPageResponse> SalariesAsync(HttpClient client, Guid companyId) =>
        client.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{companyId}/salaries", JsonOptions)!;

    private static Task<ApplicationSummaryCountsResponse> SummaryAsync(HttpClient client) =>
        client.GetFromJsonAsync<ApplicationSummaryCountsResponse>("/api/applications/summary", JsonOptions)!;
}
