using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

// The status-specific extras on the application detail (canvas "İnce dokunuşlar — Paket 2"):
// each is computed only on the status whose card uses it, and only from the caller's own rows.
public class ApplicationDetailExtrasTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = await SignInAsync("extras.test@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Waiting_Application_Carries_The_Users_Median_Reply_Time()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var days in new[] { 4, 9, 12 })
        {
            var answered = await CreateAsync(_client, $"Answered {days}", now.AddDays(-days - 1));
            await ChangeStatusAsync(_client, answered, ApplicationStatus.Screening, now.AddDays(-1));
        }

        var waiting = await CreateAsync(_client, "Waiting Co", now.AddDays(-3));
        (await DetailAsync(_client, waiting)).UserMedianResponseDays.ShouldBe(9);

        // Once answered, the application no longer waits — and its own 3 days join the sample.
        await ChangeStatusAsync(_client, waiting, ApplicationStatus.Screening, null);
        (await DetailAsync(_client, waiting)).UserMedianResponseDays.ShouldBeNull();
    }

    [Fact]
    public async Task No_Median_Below_The_Minimum_Sample()
    {
        var answered = await CreateAsync(_client, "Only One", DateTimeOffset.UtcNow.AddDays(-6));
        await ChangeStatusAsync(_client, answered, ApplicationStatus.Screening, null);

        var waiting = await CreateAsync(_client, "Waiting Co", DateTimeOffset.UtcNow.AddDays(-2));

        (await DetailAsync(_client, waiting)).UserMedianResponseDays.ShouldBeNull();
    }

    [Fact]
    public async Task A_Rejection_Carries_A_Reason_That_Recurs_In_The_Users_Own_Rejections()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await RejectAsync(_client, $"Rejecting {i}", RejectionReasonCategory.ExperienceLevelMismatch));
        }

        var detail = await DetailAsync(_client, ids[0]);

        detail.RejectionPatternCategory.ShouldBe(RejectionReasonCategory.ExperienceLevelMismatch);
        detail.RejectionPatternCount.ShouldBe(3);
        detail.RejectionPatternOutOf.ShouldBe(3);
    }

    [Fact]
    public async Task Another_Users_Rejections_Never_Make_A_Pattern()
    {
        var other = await SignInAsync("extras.other@example.com");
        for (var i = 0; i < 3; i++)
        {
            await RejectAsync(other, $"Their {i}", RejectionReasonCategory.ExperienceLevelMismatch);
        }

        var mine = await RejectAsync(_client, "Mine 1", RejectionReasonCategory.ExperienceLevelMismatch);
        await RejectAsync(_client, "Mine 2", RejectionReasonCategory.ExperienceLevelMismatch);

        (await DetailAsync(_client, mine)).RejectionPatternCategory.ShouldBeNull();
    }

    [Fact]
    public async Task An_Offer_Counts_The_Users_Other_Applications_Still_In_Interviews()
    {
        var offer = await CreateAsync(_client, "Offer Co", DateTimeOffset.UtcNow.AddDays(-20));
        await ChangeStatusAsync(_client, offer, ApplicationStatus.Offer, null);
        var interviewing = await CreateAsync(_client, "Interview Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ChangeStatusAsync(_client, interviewing, ApplicationStatus.TechnicalInterview, null);
        var screening = await CreateAsync(_client, "Screening Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ChangeStatusAsync(_client, screening, ApplicationStatus.Screening, null);
        await CreateAsync(_client, "Applied Co", DateTimeOffset.UtcNow.AddDays(-2));

        var other = await SignInAsync("extras.offer.other@example.com");
        var theirs = await CreateAsync(other, "Their Interview", DateTimeOffset.UtcNow.AddDays(-5));
        await ChangeStatusAsync(other, theirs, ApplicationStatus.Interview, null);

        (await DetailAsync(_client, offer)).OtherInterviewingCount.ShouldBe(2);
        (await DetailAsync(_client, interviewing)).OtherInterviewingCount.ShouldBeNull();
    }

    [Fact]
    public async Task A_Captured_Posting_Carries_Its_Publish_Date()
    {
        var published = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Posted Co", "Backend Developer", "https://www.linkedin.com/jobs/view/4470000001/",
                Location: null, Description: null, PublishedAt: published), JsonOptions);
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!.Application.Id;

        (await DetailAsync(_client, id)).JobPublishedAt.ShouldBe(published);
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = host.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, host.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Ex", "Tras", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string company, DateTimeOffset appliedAt)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Id;
    }

    private static async Task ChangeStatusAsync(HttpClient client, Guid id, ApplicationStatus status, DateTimeOffset? changedAt)
    {
        var response = await client.PostAsJsonAsync($"/api/applications/{id}/status",
            new ChangeStatusRequest(status, null, changedAt), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Reasons only ever arrive through the e-mail extraction, so the test writes the one
    /// column that path would have filled.</summary>
    private async Task<Guid> RejectAsync(HttpClient client, string company, RejectionReasonCategory reason)
    {
        var id = await CreateAsync(client, company, DateTimeOffset.UtcNow.AddDays(-10));
        await ChangeStatusAsync(client, id, ApplicationStatus.Rejected, null);
        await host.WithDbAsync(db => db.ApplicationStatusHistories
            .Where(h => h.ApplicationId == id && h.ToStatus == ApplicationStatus.Rejected)
            .ExecuteUpdateAsync(set => set.SetProperty(h => h.RejectionReasonCategory, reason)));
        return id;
    }

    private static async Task<ApplicationDetailResponse> DetailAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ApplicationDetailResponse>($"/api/applications/{id}", JsonOptions))!;
}
