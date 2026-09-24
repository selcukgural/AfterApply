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
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Admin;

public sealed class AdminContributionProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // Two per page so three rows make two pages, on every admin list.
        builder.UseSetting("CompanyReviews:AdminPageSize", "2");
    }
}

/// <summary>
/// The admin's salary and candidate-experience tables (2026-09-18): admin-only, newest first,
/// paged like the moderation queue, filtered by company name, each row with its author; a
/// delete removes the row for everyone at once and leaves the usual request-audit trail. Also
/// pins the moderation queue's new order — newest first — and the shared page size.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AdminContributionTests(ApiHost<AdminContributionProfile> host) : IClassFixture<ApiHost<AdminContributionProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _admin = await RegisterAsync("admin.contributions@ekariyerim.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "admin.contributions@ekariyerim.com");
        user.IsAdmin = true;
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Admin", "Tester", true));
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
            SalaryEmploymentStatus.CurrentEmployee, 95_000m, SalaryCurrency.TRY, true, 120_000m, PeriodStartYear: 2024);
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

    private Task<PagedResult<AdminCompanySalaryListItemResponse>?> SalariesAsync(string query = "") =>
        _admin.GetFromJsonAsync<PagedResult<AdminCompanySalaryListItemResponse>>($"/api/admin/company-salaries{query}", JsonOptions);

    private Task<PagedResult<AdminCandidateExperienceListItemResponse>?> ExperiencesAsync(string query = "") =>
        _admin.GetFromJsonAsync<PagedResult<AdminCandidateExperienceListItemResponse>>($"/api/admin/candidate-experiences{query}", JsonOptions);

    [Fact]
    public async Task The_Four_Routes_Are_401_Anonymous_And_403_For_An_Ordinary_User()
    {
        var ordinary = await RegisterAsync("ordinary.contributions@example.com");
        var anonymous = _factory.CreateClient();
        var id = Guid.CreateVersion7();

        foreach (var (method, path) in new (HttpMethod, string)[]
                 {
                     (HttpMethod.Get, "/api/admin/company-salaries"),
                     (HttpMethod.Delete, $"/api/admin/company-salaries/{id}"),
                     (HttpMethod.Get, "/api/admin/candidate-experiences"),
                     (HttpMethod.Delete, $"/api/admin/candidate-experiences/{id}"),
                 })
        {
            (await anonymous.SendAsync(new HttpRequestMessage(method, path))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path);
            (await ordinary.SendAsync(new HttpRequestMessage(method, path))).StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }
    }

    [Fact]
    public async Task Salaries_List_Newest_First_With_Author_Paged_And_Filtered()
    {
        var a = await RegisterAsync("salary.a@example.com");
        var b = await RegisterAsync("salary.b@example.com");
        var alpha = await ResolveAsync(a, "Admin Alpha Salaries");
        var beta = await ResolveAsync(a, "Admin Beta Salaries");
        var first = await ShareSalaryAsync(a, alpha.Id);
        await Task.Delay(20);
        var second = await ShareSalaryAsync(b, alpha.Id);
        await Task.Delay(20);
        var third = await ShareSalaryAsync(a, beta.Id);

        var page = (await SalariesAsync())!;
        page.TotalCount.ShouldBe(3);
        page.PageSize.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([third.Id, second.Id]);
        var row = page.Items.Last();
        row.AuthorEmail.ShouldBe("salary.b@example.com");
        row.CompanyName.ShouldBe("Admin Alpha Salaries");
        row.CompanySlug.ShouldBe(alpha.Slug);
        row.Occupation.Code.ShouldBe("2512");
        row.YearsOfExperience.ShouldBe(6);
        row.MonthlyNetAmount.ShouldBe(95_000m);
        row.AnnualBonusAmount.ShouldBe(120_000m);

        (await SalariesAsync("?page=2"))!.Items.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
        (await SalariesAsync("?company=beta"))!.Items.ShouldHaveSingleItem().Id.ShouldBe(third.Id);
    }

    [Fact]
    public async Task Experiences_List_Newest_First_With_Author_And_Children()
    {
        var a = await RegisterAsync("experience.a@example.com");
        var b = await RegisterAsync("experience.b@example.com");
        var company = await ResolveAsync(a, "Admin Experiences Co");
        var first = await ShareExperienceAsync(a, company.Id);
        await Task.Delay(20);
        var second = await ShareExperienceAsync(b, company.Id);

        var page = (await ExperiencesAsync())!;
        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([second.Id, first.Id]);
        var row = page.Items.First();
        row.AuthorEmail.ShouldBe("experience.b@example.com");
        row.CompanySlug.ShouldBe(company.Slug);
        row.OverallRating.ShouldBe(4);
        row.CategoryRatings.ShouldHaveSingleItem().ShouldBe(new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5));
        row.LikedStatements.ShouldBe(["communication.pos.steps_clear_upfront"]);
        row.ImprovableStatements.ShouldBe(["outcome.imp.notification"]);
        row.InterviewTypes.ShouldBe([InterviewType.Video]);
        row.Outcome.ShouldBe(HiringOutcome.Rejected);

        (await ExperiencesAsync("?company=nothing-like-this"))!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_A_Salary_Removes_It_Everywhere_And_Is_Audited()
    {
        var author = await RegisterAsync("delete.salary@example.com");
        var company = await ResolveAsync(author, "Delete Salary Co");
        var entry = await ShareSalaryAsync(author, company.Id);

        (await _admin.DeleteAsync($"/api/admin/company-salaries/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _admin.DeleteAsync($"/api/admin/company-salaries/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await author.GetFromJsonAsync<MySalariesResponse>("/api/company-salaries/mine", JsonOptions))!.Items.ShouldBeEmpty();
        (await author.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{company.Id}/salaries", JsonOptions))!.Total.ShouldBe(0);
        (await _factory.CreateClient().GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions))!
            .SalaryCount.ShouldBe(0);

        var adminId = (await _admin.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.Id;
        var audit = await SingleAuditRowAsync($"/api/admin/company-salaries/{entry.Id}");
        audit.Method.ShouldBe("DELETE");
        audit.StatusCode.ShouldBe(204);
        audit.UserId.ShouldBe(adminId);
    }

    [Fact]
    public async Task Deleting_An_Experience_Drops_The_Public_Summary_At_Once()
    {
        var author = await RegisterAsync("delete.experience@example.com");
        var company = await ResolveAsync(author, "Delete Experience Co");
        var entry = await ShareExperienceAsync(author, company.Id);
        var anonymous = _factory.CreateClient();

        // Warm the cached summary, then delete: the count must not wait for the cache to expire.
        (await anonymous.GetFromJsonAsync<CandidateExperiencePageResponse>($"/api/companies/public/{company.Slug}/experiences", JsonOptions))!
            .Summary.Count.ShouldBe(1);

        (await _admin.DeleteAsync($"/api/admin/candidate-experiences/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _admin.DeleteAsync($"/api/admin/candidate-experiences/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var page = (await anonymous.GetFromJsonAsync<CandidateExperiencePageResponse>($"/api/companies/public/{company.Slug}/experiences", JsonOptions))!;
        page.Total.ShouldBe(0);
        page.Summary.Count.ShouldBe(0);
        (await author.GetFromJsonAsync<MyCandidateExperiencesResponse>("/api/candidate-experiences/mine", JsonOptions))!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Moderation_Queue_Is_Newest_First_On_The_Shared_Page_Size()
    {
        var a = await RegisterAsync("queue.a@example.com");
        var b = await RegisterAsync("queue.b@example.com");
        var c = await RegisterAsync("queue.c@example.com");
        var company = await ResolveAsync(a, "Admin Queue Order Co");
        var first = await WriteReviewAsync(a, company.Id);
        await Task.Delay(20);
        var second = await WriteReviewAsync(b, company.Id);
        await Task.Delay(20);
        var third = await WriteReviewAsync(c, company.Id);

        var page = (await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>("/api/admin/company-reviews", JsonOptions))!;
        page.PageSize.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
        page.Items.Select(i => i.Id).ShouldBe([third.Id, second.Id]);
        (await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>("/api/admin/company-reviews?page=2", JsonOptions))!
            .Items.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
    }

    private async Task<Domain.Auditing.RequestAudit> SingleAuditRowAsync(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.RequestAudits.AsNoTracking().Where(r => r.Path == path).ToListAsync();
            if (rows.Count > 0)
            {
                // The second DELETE (404) is audited too; the first is the one that removed the row.
                return rows.OrderBy(r => r.At).First();
            }

            await Task.Delay(100);
        }

        throw new ShouldAssertException($"No request-audit row for {path}");
    }
}
