using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

public sealed class MyContributionsProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // Three per page so four rows make two pages.
        builder.UseSetting("CompanyReviews:ContributionsPageSize", "3");
    }
}

/// <summary>GET /api/contributions/mine: the author's reviews, salary entries and candidate
/// experiences as one newest-first page, each item carrying exactly one per-kind record, with
/// the three quotas; another account's rows are never in it.</summary>
[Collection(IntegrationTestCollection.Name)]
public class MyContributionsTests(ApiHost<MyContributionsProfile> host) : IClassFixture<ApiHost<MyContributionsProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Contrib", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private async Task<MyCompanyReviewResponse> WriteReviewAsync(HttpClient client, Guid companyId)
    {
        var request = new CreateCompanyReviewRequest(EmploymentStatus.FormerEmployee, 4,
            [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5)],
            ["environment.pos.team_communication"], ["pay.imp.salary_level"]);
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!;
    }

    private async Task<MyCompanySalaryResponse> ShareSalaryAsync(HttpClient client, Guid companyId, string occupationCode = "2512")
    {
        var request = new CompanySalaryRequest(Occupation.IdFor(occupationCode), 6, EmploymentType.FullTime,
            SalaryEmploymentStatus.CurrentEmployee, 95_000m, SalaryCurrency.TRY, false, PeriodStartYear: 2024);
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions))!;
    }

    private async Task<MyCandidateExperienceResponse> ShareExperienceAsync(HttpClient client, Guid companyId)
    {
        var request = new CandidateExperienceRequest(4,
            [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)],
            ["communication.pos.steps_clear_upfront"], ["outcome.imp.notification"],
            HiringOutcome.Rejected, ProcessDuration.TwoToFourWeeks, StageCount.Three, [InterviewType.Video]);
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions))!;
    }

    private static Task<MyContributionsResponse?> MineAsync(HttpClient client, int page = 1) =>
        client.GetFromJsonAsync<MyContributionsResponse>($"/api/contributions/mine?page={page}", JsonOptions);

    [Fact]
    public async Task All_Three_Kinds_Come_Back_Newest_First_In_Pages()
    {
        var author = await RegisterAsync("mine.author@example.com");
        var alpha = await ResolveAsync(author, "Mine Alpha Co");
        var beta = await ResolveAsync(author, "Mine Beta Co");

        var review = await WriteReviewAsync(author, alpha.Id);
        await Task.Delay(20);
        var firstSalary = await ShareSalaryAsync(author, alpha.Id);
        await Task.Delay(20);
        var experience = await ShareExperienceAsync(author, beta.Id);
        await Task.Delay(20);
        var secondSalary = await ShareSalaryAsync(author, beta.Id, "2513");

        var first = (await MineAsync(author))!;
        first.TotalCount.ShouldBe(4);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(3);
        first.Items.Select(i => i.Kind).ShouldBe([ContributionKind.Salary, ContributionKind.Experience, ContributionKind.Salary]);
        first.Items[0].Salary!.Id.ShouldBe(secondSalary.Id);
        first.Items[0].Review.ShouldBeNull();
        first.Items[0].Experience.ShouldBeNull();
        // The stamp and the hydrated row come from the same database read, so they agree exactly;
        // the create response carried the in-memory timestamp, which keeps 100 ns ticks that
        // Postgres rounds to microseconds — compare that one with a tolerance.
        first.Items[0].SubmittedAt.ShouldBe(first.Items[0].Salary!.SubmittedAt);
        first.Items[0].SubmittedAt.ShouldBe(secondSalary.SubmittedAt, TimeSpan.FromMilliseconds(1));
        first.Items[1].Experience!.Id.ShouldBe(experience.Id);
        first.Items[1].Experience!.CompanySlug.ShouldBe(beta.Slug);
        first.Items[2].Salary!.Id.ShouldBe(firstSalary.Id);

        var second = (await MineAsync(author, page: 2))!;
        second.Items.ShouldHaveSingleItem().Kind.ShouldBe(ContributionKind.Review);
        second.Items[0].Review!.Id.ShouldBe(review.Id);
        second.Items[0].Review!.Status.ShouldBe(ReviewModerationStatus.Approved);

        first.ReviewQuota.Used.ShouldBe(1);
        first.SalaryQuota.ShouldNotBeNull().Used.ShouldBe(2);
        first.ExperienceQuota.ShouldNotBeNull().Used.ShouldBe(1);
    }

    [Fact]
    public async Task Another_Account_Sees_None_Of_It()
    {
        var author = await RegisterAsync("mine.owner@example.com");
        var other = await RegisterAsync("mine.other@example.com");
        var company = await ResolveAsync(author, "Mine Private Co");
        await ShareSalaryAsync(author, company.Id);
        await ShareExperienceAsync(author, company.Id);

        var mine = (await MineAsync(other))!;
        mine.TotalCount.ShouldBe(0);
        mine.Items.ShouldBeEmpty();
        mine.ReviewQuota.Used.ShouldBe(0);
    }

    [Fact]
    public async Task Past_The_End_Is_An_Empty_Page_And_Anonymous_Is_401()
    {
        var author = await RegisterAsync("mine.paging@example.com");
        var company = await ResolveAsync(author, "Mine Paging Co");
        await ShareSalaryAsync(author, company.Id);

        var far = (await MineAsync(author, page: 9))!;
        far.TotalCount.ShouldBe(1);
        far.Items.ShouldBeEmpty();
        (await author.GetAsync("/api/contributions/mine?page=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _factory.CreateClient().GetAsync("/api/contributions/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
