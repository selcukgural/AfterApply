using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

/// <summary>
/// The "backed by a tracked application" label (2026-09-24, research item #7): an experience
/// needs any application of the author's at that company, a salary or a review an Accepted one,
/// and in every case the application must have been recorded here at least 14 days before the
/// contribution — measured on the server's own timestamps, never on the dates the client sends.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ContributionProofTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Proof", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    /// <summary>An application at <paramref name="company"/>, its server-side record time moved
    /// <paramref name="recordedDaysAgo"/> days back. The applied date the client sends is a year
    /// ago on purpose: it must not count.</summary>
    private async Task<(Guid ApplicationId, Guid CompanyId)> TrackAsync(HttpClient client, string company, double recordedDaysAgo,
        ApplicationStatus? status = null)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Backend Developer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-365), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
        if (status is not null)
        {
            (await client.PostAsJsonAsync($"/api/applications/{created.Id}/status",
                new ChangeStatusRequest(status.Value, null, null), JsonOptions)).EnsureSuccessStatusCode();
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recordedAt = DateTimeOffset.UtcNow.AddDays(-recordedDaysAgo);
        await db.Applications.Where(a => a.Id == created.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, recordedAt));
        return (created.Id, created.CompanyId);
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private static async Task ShareExperienceAsync(HttpClient client, Guid companyId)
    {
        var request = new CandidateExperienceRequest(4,
            [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)],
            ["communication.pos.steps_clear_upfront"], ["outcome.imp.notification"],
            HiringOutcome.Rejected, ProcessDuration.TwoToFourWeeks, StageCount.Three, [InterviewType.Video]);
        (await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences", request, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task ShareSalaryAsync(HttpClient client, Guid companyId)
    {
        var request = new CompanySalaryRequest(Occupation.IdFor("2512"), 6, EmploymentType.FullTime,
            SalaryEmploymentStatus.CurrentEmployee, 95_000m, SalaryCurrency.TRY, true, 120_000m, PeriodStartYear: 2024);
        (await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries", request, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task WriteReviewAsync(HttpClient client, Guid companyId)
    {
        var request = new CreateCompanyReviewRequest(EmploymentStatus.CurrentEmployee, 4,
            [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5)],
            ["environment.pos.team_communication"], ["pay.imp.salary_level"]);
        (await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", request, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<IReadOnlyList<CandidateExperiencePublicResponse>> PublicExperiencesAsync(string slug) =>
        (await _factory.CreateClient().GetFromJsonAsync<CandidateExperiencePageResponse>(
            $"/api/companies/public/{slug}/experiences", JsonOptions))!.Items;

    private async Task<IReadOnlyCollection<CompanyReviewPublicResponse>> PublicReviewsAsync(string slug) =>
        (await _factory.CreateClient().GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{slug}/reviews", JsonOptions))!.Items;

    private static async Task<IReadOnlyList<CompanySalaryPublicResponse>> SalariesAsync(HttpClient reader, Guid companyId) =>
        (await reader.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{companyId}/salaries", JsonOptions))!.Items;

    private static async Task<IReadOnlyList<MyContributionResponse>> MineAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine", JsonOptions))!.Items;

    [Fact]
    public async Task An_Experience_Is_Backed_By_Any_Application_Recorded_Two_Weeks_Before_It()
    {
        var author = await RegisterAsync("proof.experience@example.com");
        var (_, companyId) = await TrackAsync(author, "Proof Backed Co", recordedDaysAgo: 15, ApplicationStatus.Rejected);
        var company = await ResolveAsync(author, "Proof Backed Co");
        company.Id.ShouldBe(companyId);

        await ShareExperienceAsync(author, companyId);

        (await PublicExperiencesAsync(company.Slug)).ShouldHaveSingleItem().BackedByApplication.ShouldBeTrue();
        (await MineAsync(author)).ShouldHaveSingleItem().BackedByApplication.ShouldBeTrue();
    }

    [Fact]
    public async Task An_Application_Recorded_Less_Than_Two_Weeks_Before_Does_Not_Back_It_Whatever_Its_Applied_Date()
    {
        // Applied "a year ago" by the client's say-so, but recorded here 13 days before writing.
        var author = await RegisterAsync("proof.recent@example.com");
        var (_, companyId) = await TrackAsync(author, "Proof Recent Co", recordedDaysAgo: 13);
        var company = await ResolveAsync(author, "Proof Recent Co");

        await ShareExperienceAsync(author, companyId);

        (await PublicExperiencesAsync(company.Slug)).ShouldHaveSingleItem().BackedByApplication.ShouldBeFalse();
        (await MineAsync(author)).ShouldHaveSingleItem().BackedByApplication.ShouldBeFalse();
    }

    [Fact]
    public async Task Someone_Elses_Application_Or_One_At_Another_Company_Does_Not_Back_It()
    {
        var author = await RegisterAsync("proof.author@example.com");
        var other = await RegisterAsync("proof.other@example.com");
        var (_, companyId) = await TrackAsync(other, "Proof Shared Co", recordedDaysAgo: 60);
        await TrackAsync(author, "Proof Elsewhere Co", recordedDaysAgo: 60);
        var company = await ResolveAsync(author, "Proof Shared Co");

        await ShareExperienceAsync(author, companyId);

        (await PublicExperiencesAsync(company.Slug)).ShouldHaveSingleItem().BackedByApplication.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Salary_And_A_Review_Need_An_Accepted_Application()
    {
        var hired = await RegisterAsync("proof.hired@example.com");
        var rejected = await RegisterAsync("proof.rejected@example.com");
        var (_, companyId) = await TrackAsync(hired, "Proof Employer Co", recordedDaysAgo: 90, ApplicationStatus.Accepted);
        await TrackAsync(rejected, "Proof Employer Co", recordedDaysAgo: 90, ApplicationStatus.Rejected);
        var company = await ResolveAsync(hired, "Proof Employer Co");

        await ShareSalaryAsync(hired, companyId);
        await WriteReviewAsync(hired, companyId);
        await ShareSalaryAsync(rejected, companyId);
        await WriteReviewAsync(rejected, companyId);

        (await SalariesAsync(hired, companyId)).Select(s => s.BackedByApplication).Order().ShouldBe([false, true]);
        (await PublicReviewsAsync(company.Slug)).Select(r => r.BackedByApplication).Order().ShouldBe([false, true]);
        (await MineAsync(hired)).ShouldAllBe(c => c.BackedByApplication);
        (await MineAsync(rejected)).ShouldAllBe(c => !c.BackedByApplication);
    }

    [Fact]
    public async Task Deleting_The_Application_Takes_The_Label_Off()
    {
        var author = await RegisterAsync("proof.delete@example.com");
        var (applicationId, companyId) = await TrackAsync(author, "Proof Deleted Co", recordedDaysAgo: 30);
        await ShareExperienceAsync(author, companyId);
        (await MineAsync(author)).ShouldHaveSingleItem().BackedByApplication.ShouldBeTrue();

        (await author.DeleteAsync($"/api/applications/{applicationId}")).IsSuccessStatusCode.ShouldBeTrue();

        (await MineAsync(author)).ShouldHaveSingleItem().BackedByApplication.ShouldBeFalse();
    }

    [Fact]
    public async Task The_Public_Record_Carries_A_Yes_Or_No_And_Nothing_About_The_Application()
    {
        var author = await RegisterAsync("proof.wire@example.com");
        var (applicationId, companyId) = await TrackAsync(author, "Proof Wire Co", recordedDaysAgo: 20);
        var company = await ResolveAsync(author, "Proof Wire Co");
        await ShareExperienceAsync(author, companyId);

        var json = await _factory.CreateClient().GetStringAsync($"/api/companies/public/{company.Slug}/experiences");

        json.ShouldContain("\"backedByApplication\":true");
        json.ShouldNotContain(applicationId.ToString());
        json.ShouldNotContain("applicationId", Case.Insensitive);
    }
}
