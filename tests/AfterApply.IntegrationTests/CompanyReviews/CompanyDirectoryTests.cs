using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
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

public sealed class CompanyDirectoryProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CompanyReviews:PublicPageSize", "3");
    }
}

/// <summary>
/// The public directory since 2026-09-18: a company is listed once it has any published
/// contribution — review, salary entry or candidate experience — its card carries the three
/// counts, and the company that got a contribution most recently comes first. The sitemap's slug
/// list follows the same rule minus salaries, which a crawler cannot read.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyDirectoryTests(ApiHost<CompanyDirectoryProfile> host) : IClassFixture<ApiHost<CompanyDirectoryProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Directory", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private static CreateCompanyReviewRequest Review() => new(
        EmploymentStatus.FormerEmployee, 4,
        [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5)],
        ["environment.pos.team_communication"], ["pay.imp.salary_level"]);

    private static CompanySalaryRequest Salary() => new(Occupation.IdFor("2512"), 6, EmploymentType.FullTime,
        SalaryEmploymentStatus.CurrentEmployee, 95_000m, SalaryCurrency.TRY, false);

    private static CandidateExperienceRequest Experience() => new(4,
        [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)],
        ["communication.pos.steps_clear_upfront"], ["outcome.imp.notification"],
        HiringOutcome.Rejected, ProcessDuration.TwoToFourWeeks, StageCount.Three, [InterviewType.Video]);

    private async Task<MyCompanyReviewResponse> WriteReviewAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", Review(), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!;
    }

    private async Task ShareSalaryAsync(HttpClient client, Guid companyId) =>
        (await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries", Salary(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Created);

    private async Task ShareExperienceAsync(HttpClient client, Guid companyId) =>
        (await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences", Experience(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Created);

    private async Task<PagedResult<CompanyPublicListItemResponse>> DirectoryAsync(string? q = null, int page = 1)
    {
        var query = q is null ? $"?page={page}" : $"?q={Uri.EscapeDataString(q)}&page={page}";
        return (await _factory.CreateClient().GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>($"/api/companies/public{query}", JsonOptions))!;
    }

    private async Task<IReadOnlyList<ReviewedCompanySlugResponse>> SlugsAsync() =>
        (await _factory.CreateClient().GetFromJsonAsync<IReadOnlyList<ReviewedCompanySlugResponse>>("/api/companies/public/slugs", JsonOptions))!;

    [Fact]
    public async Task A_Salary_Alone_Puts_The_Company_On_The_List_Without_A_Score()
    {
        var author = await RegisterAsync("salary.only@example.com");
        var company = await ResolveAsync(author, "Salary Only Directory Co");
        await ShareSalaryAsync(author, company.Id);

        var item = (await DirectoryAsync("salary only")).Items.ShouldHaveSingleItem();
        item.Slug.ShouldBe(company.Slug);
        item.ApprovedCount.ShouldBe(0);
        item.Score.ShouldBeNull();
        item.SalaryCount.ShouldBe(1);
        item.CandidateExperienceCount.ShouldBe(0);

        // A crawler cannot read salaries, so the sitemap does not send it there.
        (await SlugsAsync()).ShouldNotContain(s => s.Slug == company.Slug);
    }

    [Fact]
    public async Task An_Experience_Alone_Puts_The_Company_On_The_List_And_In_The_Sitemap()
    {
        var author = await RegisterAsync("experience.only@example.com");
        var company = await ResolveAsync(author, "Experience Only Directory Co");
        await ShareExperienceAsync(author, company.Id);

        var item = (await DirectoryAsync("experience only")).Items.ShouldHaveSingleItem();
        item.Slug.ShouldBe(company.Slug);
        item.ApprovedCount.ShouldBe(0);
        item.SalaryCount.ShouldBe(0);
        item.CandidateExperienceCount.ShouldBe(1);

        (await SlugsAsync()).ShouldContain(s => s.Slug == company.Slug);
    }

    [Fact]
    public async Task The_Card_Counts_All_Three_Kinds()
    {
        var a = await RegisterAsync("three.a@example.com");
        var b = await RegisterAsync("three.b@example.com");
        var company = await ResolveAsync(a, "Three Kinds Directory Co");
        await WriteReviewAsync(a, company.Id);
        await ShareSalaryAsync(a, company.Id);
        await ShareSalaryAsync(b, company.Id);
        await ShareExperienceAsync(b, company.Id);

        var item = (await DirectoryAsync("three kinds")).Items.ShouldHaveSingleItem();
        item.ApprovedCount.ShouldBe(1);
        item.SalaryCount.ShouldBe(2);
        item.CandidateExperienceCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_Company_Nobody_Contributed_To_Is_Not_Listed()
    {
        var author = await RegisterAsync("nothing.yet@example.com");
        await ResolveAsync(author, "Nothing Yet Directory Co");

        (await DirectoryAsync("nothing yet")).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Most_Recently_Contributed_To_Company_Comes_First()
    {
        var author = await RegisterAsync("order.author@example.com");
        var alpha = await ResolveAsync(author, "Order Alpha Directory");
        var beta = await ResolveAsync(author, "Order Beta Directory");
        var gamma = await ResolveAsync(author, "Order Gamma Directory");

        var review = await WriteReviewAsync(author, alpha.Id);
        await Task.Delay(20);
        await ShareSalaryAsync(author, beta.Id);
        await Task.Delay(20);
        await ShareExperienceAsync(author, gamma.Id);

        (await DirectoryAsync("order")).Items.Select(i => i.Slug).ShouldBe([gamma.Slug, beta.Slug, alpha.Slug]);

        // Editing a review re-stamps it, so alpha becomes the latest again.
        await Task.Delay(20);
        var update = new UpdateCompanyReviewRequest(EmploymentStatus.CurrentEmployee, 5,
            [new ReviewCategoryRatingDto(ReviewCategory.Management, 4)], ["management.pos.feedback_culture"], []);
        (await author.PutAsJsonAsync($"/api/company-reviews/{review.Id}", update, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DirectoryAsync("order")).Items.Select(i => i.Slug).ShouldBe([alpha.Slug, gamma.Slug, beta.Slug]);
    }

    [Fact]
    public async Task The_List_Pages_And_Filters_By_Name()
    {
        var author = await RegisterAsync("paging.author@example.com");
        foreach (var name in new[] { "Paging One Co", "Paging Two Co", "Paging Three Co", "Paging Four Co" })
        {
            await ShareSalaryAsync(author, (await ResolveAsync(author, name)).Id);
        }

        var first = await DirectoryAsync("paging");
        first.TotalCount.ShouldBe(4);
        first.PageSize.ShouldBe(3);
        first.Items.Count.ShouldBe(3);
        (await DirectoryAsync("paging", page: 2)).Items.ShouldHaveSingleItem();
        (await DirectoryAsync("paging two")).Items.ShouldHaveSingleItem().Name.ShouldBe("Paging Two Co");
    }
}
