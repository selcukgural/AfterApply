using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CandidateExperiences;

public sealed class ExperienceInviteProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // The defaults, pinned: the tests below are written against four weeks, a year and three.
        builder.UseSetting("CandidateExperiences:InviteDelayDays", "28");
        builder.UseSetting("CandidateExperiences:InviteMaxAgeDays", "365");
        builder.UseSetting("CandidateExperiences:InviteLimit", "3");
    }
}

/// <summary>
/// The dashboard's ended-processes card (contribution loop #10, 2026-09-24): which closed
/// applications it lists, when, how many, and what takes one off — rating the company,
/// dismissing it — plus ownership and the account export.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ExperienceInviteTests(ApiHost<ExperienceInviteProfile> host)
    : IClassFixture<ApiHost<ExperienceInviteProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset DaysAgo(int days) => DateTimeOffset.UtcNow.AddDays(-days);

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Invite", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    /// <summary>An application at <paramref name="company"/> that ended <paramref name="endedDaysAgo"/>
    /// days ago in <paramref name="status"/>; null leaves it open.</summary>
    private static async Task<(Guid ApplicationId, Guid CompanyId)> ProcessAsync(HttpClient client, string company,
        ApplicationStatus? status, int endedDaysAgo, int appliedDaysAgo = 500)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Backend Developer", null, null, EmploymentType.FullTime, DaysAgo(appliedDaysAgo), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
        if (status is not null)
        {
            (await client.PostAsJsonAsync($"/api/applications/{created.Id}/status",
                new ChangeStatusRequest(status.Value, null, DaysAgo(endedDaysAgo)), JsonOptions)).EnsureSuccessStatusCode();
        }

        return (created.Id, created.CompanyId);
    }

    private static async Task<IReadOnlyList<ExperienceInviteResponse>> InvitesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ExperienceInviteResponse>>("/api/experience-invites", JsonOptions))!;

    [Fact]
    public async Task A_Closed_Process_Is_Asked_About_Only_After_Four_Weeks_And_Within_A_Year()
    {
        var client = await RegisterAsync("invite.timing@example.com");
        await ProcessAsync(client, "Ripe Co", ApplicationStatus.Rejected, endedDaysAgo: 30);
        await ProcessAsync(client, "Fresh Co", ApplicationStatus.Rejected, endedDaysAgo: 10);
        await ProcessAsync(client, "Stale Co", ApplicationStatus.Rejected, endedDaysAgo: 400, appliedDaysAgo: 450);

        var invite = (await InvitesAsync(client)).ShouldHaveSingleItem();
        invite.CompanyName.ShouldBe("Ripe Co");
        invite.CompanySlug.ShouldNotBeNullOrWhiteSpace();
        invite.JobTitle.ShouldBe("Backend Developer");
        invite.Outcome.ShouldBe(ApplicationStatus.Rejected);
        invite.EndedAt.ShouldBe(DaysAgo(30), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Rejections_Silences_And_Hires_Are_Asked_About_Withdrawals_And_Open_Processes_Are_Not()
    {
        var client = await RegisterAsync("invite.statuses@example.com");
        await ProcessAsync(client, "Said No Co", ApplicationStatus.Rejected, endedDaysAgo: 40);
        await ProcessAsync(client, "Went Quiet Co", ApplicationStatus.Ghosted, endedDaysAgo: 45);
        await ProcessAsync(client, "Hired Me Co", ApplicationStatus.Accepted, endedDaysAgo: 50);
        await ProcessAsync(client, "I Withdrew Co", ApplicationStatus.Withdrawn, endedDaysAgo: 40);
        await ProcessAsync(client, "Still Open Co", ApplicationStatus.Interview, endedDaysAgo: 40);

        var invites = await InvitesAsync(client);
        invites.Select(i => i.CompanyName).ShouldBe(["Said No Co", "Went Quiet Co", "Hired Me Co"]);
        invites.Select(i => i.Outcome).ShouldBe([ApplicationStatus.Rejected, ApplicationStatus.Ghosted, ApplicationStatus.Accepted]);
    }

    [Fact]
    public async Task One_Line_Per_Company_Newest_First_At_Most_Three()
    {
        var client = await RegisterAsync("invite.limit@example.com");
        await ProcessAsync(client, "Twice Co", ApplicationStatus.Rejected, endedDaysAgo: 90);
        await ProcessAsync(client, "Twice Co", ApplicationStatus.Ghosted, endedDaysAgo: 35);
        await ProcessAsync(client, "Second Co", ApplicationStatus.Rejected, endedDaysAgo: 60);
        await ProcessAsync(client, "Third Co", ApplicationStatus.Rejected, endedDaysAgo: 70);
        await ProcessAsync(client, "Fourth Co", ApplicationStatus.Rejected, endedDaysAgo: 80);

        var invites = await InvitesAsync(client);
        invites.Select(i => i.CompanyName).ShouldBe(["Twice Co", "Second Co", "Third Co"]);
        invites[0].Outcome.ShouldBe(ApplicationStatus.Ghosted);
    }

    [Fact]
    public async Task Rating_The_Company_Or_Dismissing_It_Takes_It_Off_For_Good()
    {
        var client = await RegisterAsync("invite.done@example.com");
        var (_, rated) = await ProcessAsync(client, "Rated Co", ApplicationStatus.Rejected, endedDaysAgo: 40);
        var (_, dismissed) = await ProcessAsync(client, "Dismissed Co", ApplicationStatus.Rejected, endedDaysAgo: 40);
        (await InvitesAsync(client)).Count.ShouldBe(2);

        (await client.PostAsJsonAsync($"/api/companies/{rated}/experiences", new CandidateExperienceRequest(4), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsync($"/api/experience-invites/{dismissed}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Idempotent: a second tab dismissing the same company is not an error.
        (await client.PostAsync($"/api/experience-invites/{dismissed}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await InvitesAsync(client)).ShouldBeEmpty();

        // A later process at the dismissed company is not asked about either: the "no" is per company.
        await ProcessAsync(client, "Dismissed Co", ApplicationStatus.Ghosted, endedDaysAgo: 30);
        (await InvitesAsync(client)).ShouldBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ExperienceInviteDismissals.CountAsync(d => d.CompanyId == dismissed)).ShouldBe(1);
    }

    [Fact]
    public async Task Each_Account_Sees_And_Dismisses_Only_Its_Own()
    {
        var owner = await RegisterAsync("invite.owner@example.com");
        var stranger = await RegisterAsync("invite.stranger@example.com");
        var (_, companyId) = await ProcessAsync(owner, "Private Co", ApplicationStatus.Rejected, endedDaysAgo: 40);

        (await InvitesAsync(stranger)).ShouldBeEmpty();
        // A company the caller never applied to is "not found", whoever else applied there.
        (await stranger.PostAsync($"/api/experience-invites/{companyId}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/experience-invites/{Guid.NewGuid()}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await InvitesAsync(owner)).ShouldHaveSingleItem();

        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/experience-invites")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/experience-invites/{companyId}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_Export_Lists_Dismissals_And_Deleting_The_Account_Removes_Them()
    {
        var client = await RegisterAsync("invite.export@example.com");
        var (_, companyId) = await ProcessAsync(client, "Export Invite Co", ApplicationStatus.Rejected, endedDaysAgo: 40);
        (await client.PostAsync($"/api/experience-invites/{companyId}/dismiss", null)).EnsureSuccessStatusCode();

        var export = (await client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions))!;
        var row = export.ExperienceInviteDismissals.ShouldNotBeNull().ShouldHaveSingleItem();
        row.CompanyId.ShouldBe(companyId);
        row.CompanyName.ShouldBe("Export Invite Co");

        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ExperienceInviteDismissals.AnyAsync(d => d.CompanyId == companyId)).ShouldBeFalse();
    }
}
