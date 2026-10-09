using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Notifications;

// "Remind me to apply here again" on a rejected application, and the interviewer names the
// thank-you draft greets (canvas "İnce dokunuşlar — Paket 6"). The HTTP calls run at the start of
// the clock (their tokens are checked against the wall clock); the due list and the sweeps are
// called on the services with the clock moved months on.
public class ReapplyReminderTests(ApiHost<ReminderDeferralProfile> host)
    : IClassFixture<ApiHost<ReminderDeferralProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = await ClientAsync("reapply.test@example.com");
        _userId = await host.WithDbAsync(db => db.Users.Where(u => u.Email == "reapply.test@example.com").Select(u => u.Id).SingleAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Reminder_Set_On_A_Rejection_Shows_On_Its_Day_And_Nothing_Else_Touches_It()
    {
        var id = await CreateAsync("Lodos Co");
        await ChangeStatusAsync(id, ApplicationStatus.Rejected);

        var set = await _client.PutAsJsonAsync($"/api/applications/{id}/reapply-reminder", new SetReapplyReminderRequest(6), JsonOptions);
        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var remindAt = (await set.Content.ReadFromJsonAsync<ReapplyReminderStateResponse>(JsonOptions))!.RemindAt!.Value;
        remindAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMonths(6).AddDays(-1));

        var detail = await DetailAsync(id);
        detail.ReapplyDecided.ShouldBeTrue();
        detail.ReapplyRemindAt.ShouldNotBeNull().ShouldBe(remindAt, TimeSpan.FromMilliseconds(1));

        (await DueAtAsync(remindAt.AddMinutes(-1))).ShouldBeEmpty();

        // On its day: the follow-up list, its "close all" and the nightly sweep all leave it be.
        var due = remindAt.AddMinutes(5);
        await host.WithScopeAsync(async services =>
        {
            var reminders = services.GetRequiredService<IReminderService>();
            await reminders.ScanAndGenerateRemindersAsync(CancellationToken.None);
            await reminders.BulkDismissAsync(_userId, new BulkReminderRequest(new ReminderSelection(All: true)), CancellationToken.None);
            (await reminders.GetActiveRemindersAsync(_userId, new GetRemindersQuery(1, 50), CancellationToken.None)).Items.ShouldBeEmpty();
        });

        var shown = (await DueAtAsync(due)).ShouldHaveSingleItem();
        shown.ApplicationId.ShouldBe(id);
        shown.CompanyName.ShouldBe("Lodos Co");
    }

    [Fact]
    public async Task Declining_Answers_The_Question_And_Cancelling_Takes_The_Reminder_Away()
    {
        var declined = await CreateAsync("No Thanks Co");
        await ChangeStatusAsync(declined, ApplicationStatus.Rejected);
        (await _client.DeleteAsync($"/api/applications/{declined}/reapply-reminder")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var declinedDetail = await DetailAsync(declined);
        declinedDetail.ReapplyDecided.ShouldBeTrue();
        declinedDetail.ReapplyRemindAt.ShouldBeNull();

        var cancelled = await CreateAsync("Changed Mind Co");
        await ChangeStatusAsync(cancelled, ApplicationStatus.Rejected);
        (await _client.PutAsJsonAsync($"/api/applications/{cancelled}/reapply-reminder", new SetReapplyReminderRequest(3), JsonOptions))
            .EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/applications/{cancelled}/reapply-reminder")).EnsureSuccessStatusCode();

        (await DetailAsync(cancelled)).ReapplyRemindAt.ShouldBeNull();
        (await DueAtAsync(DateTimeOffset.UtcNow.AddMonths(4))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_The_Owner_Of_A_Rejected_Application_Can_Set_One_And_Only_For_The_Offered_Lengths()
    {
        var open = await CreateAsync("Still Open Co");
        (await _client.PutAsJsonAsync($"/api/applications/{open}/reapply-reminder", new SetReapplyReminderRequest(6), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await ChangeStatusAsync(open, ApplicationStatus.Rejected);
        (await _client.PutAsJsonAsync($"/api/applications/{open}/reapply-reminder", new SetReapplyReminderRequest(4), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var stranger = await ClientAsync("reapply.stranger@example.com");
        (await stranger.PutAsJsonAsync($"/api/applications/{open}/reapply-reminder", new SetReapplyReminderRequest(6), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync($"/api/applications/{open}/reapply-reminder")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.WithDbAsync(db => db.Reminders.CountAsync(r => r.Type == ReminderType.Reapply))).ShouldBe(0);
    }

    [Fact]
    public async Task A_Reminder_Of_An_Earlier_Rejection_Stays_Quiet_After_The_Next_One()
    {
        var id = await CreateAsync("Twice Co");
        await ChangeStatusAsync(id, ApplicationStatus.Rejected);
        (await _client.PutAsJsonAsync($"/api/applications/{id}/reapply-reminder", new SetReapplyReminderRequest(3), JsonOptions))
            .EnsureSuccessStatusCode();

        await ChangeStatusAsync(id, ApplicationStatus.Interview);
        await ChangeStatusAsync(id, ApplicationStatus.Rejected);

        (await DueAtAsync(DateTimeOffset.UtcNow.AddMonths(4))).ShouldBeEmpty();
        var detail = await DetailAsync(id);
        detail.ReapplyDecided.ShouldBeFalse(); // a new rejection asks afresh
    }

    [Fact]
    public async Task Interviewer_Names_Ride_With_The_Interview_And_Leave_With_The_Export()
    {
        var id = await CreateAsync("Names Co");
        var at = DateTimeOffset.UtcNow.AddDays(3);
        var changed = await _client.PostAsJsonAsync($"/api/applications/{id}/status", new ChangeStatusRequest(
            ApplicationStatus.Interview, null, null, InterviewAt: at, InterviewFormat: InterviewFormat.Online,
            InterviewWith: "  Ece Kaya, Burak Demir  "), JsonOptions);
        changed.EnsureSuccessStatusCode();
        (await DetailAsync(id)).InterviewWith.ShouldBe("Ece Kaya, Burak Demir");

        var export = (await _client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions))!;
        export.Applications.Single(a => a.Id == id).InterviewWith.ShouldBe("Ece Kaya, Burak Demir");

        (await _client.PutAsJsonAsync($"/api/applications/{id}/interview",
            new SetInterviewRequest(at, InterviewFormat.InPerson, new string('x', 201)), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await _client.PutAsJsonAsync($"/api/applications/{id}/interview", new SetInterviewRequest(null), JsonOptions))
            .EnsureSuccessStatusCode();
        (await DetailAsync(id)).InterviewWith.ShouldBeNull();
        (await host.WithDbAsync(db => db.Applications.Where(a => a.Id == id).Select(a => a.InterviewWith).SingleAsync())).ShouldBeNull();
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = host.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, host.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Re", "Apply", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<Guid> CreateAsync(string company)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-10), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Id;
    }

    private async Task ChangeStatusAsync(Guid id, ApplicationStatus status) =>
        (await _client.PostAsJsonAsync($"/api/applications/{id}/status", new ChangeStatusRequest(status, null, null), JsonOptions))
        .EnsureSuccessStatusCode();

    private async Task<ApplicationDetailResponse> DetailAsync(Guid id) =>
        (await _client.GetFromJsonAsync<ApplicationDetailResponse>($"/api/applications/{id}", JsonOptions))!;

    private async Task<IReadOnlyList<ReapplyReminderResponse>> DueAtAsync(DateTimeOffset at)
    {
        host.Profile.Clock.Set(at);
        IReadOnlyList<ReapplyReminderResponse> items = [];
        await host.WithScopeAsync(async services =>
            items = await services.GetRequiredService<IReminderService>().GetDueReapplyRemindersAsync(_userId, CancellationToken.None));
        host.Profile.Clock.Reset();
        return items;
    }
}
