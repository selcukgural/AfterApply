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
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Notifications;

/// <summary>The interview date on an application and everything built on it: the upcoming list, the
/// "how did it go?" reminder, its answers and their undo, and snoozing a reminder.</summary>
public class InterviewReminderTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = await CreateAuthenticatedClientAsync("interviews.test@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = host.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, host.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Interview", "Test", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    /// <summary>An application that reached <paramref name="status"/> <paramref name="stageDaysAgo"/>
    /// days ago, after applying a few days before that.</summary>
    private async Task<Guid> CreateApplicationInStageAsync(string companyName, ApplicationStatus status = ApplicationStatus.Interview,
        int stageDaysAgo = 3, HttpClient? client = null)
    {
        client ??= _client;
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Backend Developer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-stageDaysAgo - 5), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Id;

        var status2 = await client.PostAsJsonAsync($"/api/applications/{id}/status",
            new ChangeStatusRequest(status, null, DateTimeOffset.UtcNow.AddDays(-stageDaysAgo)), JsonOptions);
        status2.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<HttpResponseMessage> PutInterviewAsync(Guid applicationId, DateTimeOffset? at,
        InterviewFormat? format = null, HttpClient? client = null) =>
        await (client ?? _client).PutAsJsonAsync($"/api/applications/{applicationId}/interview",
            new SetInterviewRequest(at, format), JsonOptions);

    private async Task<ApplicationDetailResponse> GetApplicationAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    private async Task ScanAsync()
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReminderService>().ScanAndGenerateRemindersAsync(CancellationToken.None);
    }

    private async Task<List<ReminderResponse>> GetRemindersAsync(HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync("/api/reminders?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return [.. (await response.Content.ReadFromJsonAsync<PagedResult<ReminderResponse>>(JsonOptions))!.Items];
    }

    private async Task<List<UpcomingInterviewResponse>> GetUpcomingAsync(HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync("/api/reminders/interviews");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<UpcomingInterviewResponse>>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> AnswerAsync(Guid reminderId, InterviewOutcomeRequest request, HttpClient? client = null) =>
        await (client ?? _client).PostAsJsonAsync($"/api/reminders/{reminderId}/interview-outcome", request, JsonOptions);

    /// <summary>An application whose interview ended a few hours ago, scanned: its reminder is the
    /// "how did it go?" row.</summary>
    private async Task<(Guid ApplicationId, ReminderResponse Reminder)> HeldInterviewAsync(string companyName,
        ApplicationStatus status = ApplicationStatus.Interview)
    {
        var applicationId = await CreateApplicationInStageAsync(companyName, status);
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddHours(-3))).EnsureSuccessStatusCode();
        await ScanAsync();
        var reminder = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);
        return (applicationId, reminder);
    }

    private async Task<Reminder> GetReminderRowAsync(Guid reminderId)
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Reminders.AsNoTracking().SingleAsync(r => r.Id == reminderId);
    }

    [Fact]
    public async Task A_Status_Change_Can_Carry_The_Interview_And_The_Page_Reads_It_Back()
    {
        var applicationId = await CreateApplicationInStageAsync("Carry Co", ApplicationStatus.Screening);
        var at = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(14), TimeSpan.FromHours(3));

        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.TechnicalInterview, null, null, InterviewAt: at,
                InterviewFormat: InterviewFormat.InPerson), JsonOptions);
        response.EnsureSuccessStatusCode();

        var detail = await GetApplicationAsync(applicationId);
        detail.Status.ShouldBe(ApplicationStatus.TechnicalInterview);
        detail.InterviewAt.ShouldBe(at);
        detail.InterviewFormat.ShouldBe(InterviewFormat.InPerson);

        var timeline = await _client.GetFromJsonAsync<List<ApplicationEventResponse>>(
            $"/api/applications/{applicationId}/timeline", JsonOptions);
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.InterviewScheduled);
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.StatusChanged);
    }

    [Fact]
    public async Task A_Status_Change_Out_Of_Interview_Stages_Cannot_Carry_One()
    {
        var applicationId = await CreateApplicationInStageAsync("Offer Co");

        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.Offer, null, null, InterviewAt: DateTimeOffset.UtcNow.AddDays(1)), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.Interview);
    }

    [Fact]
    public async Task The_Interview_Is_Set_Moved_And_Cleared_On_Its_Own()
    {
        var applicationId = await CreateApplicationInStageAsync("Own Co");
        var at = DateTimeOffset.UtcNow.AddDays(3);

        (await PutInterviewAsync(applicationId, at, InterviewFormat.Phone)).EnsureSuccessStatusCode();
        (await GetApplicationAsync(applicationId)).InterviewFormat.ShouldBe(InterviewFormat.Phone);

        (await PutInterviewAsync(applicationId, at.AddDays(1), InterviewFormat.Phone)).EnsureSuccessStatusCode();
        (await GetApplicationAsync(applicationId)).InterviewAt!.Value.ShouldBe(at.AddDays(1), TimeSpan.FromMilliseconds(1));

        (await PutInterviewAsync(applicationId, null)).EnsureSuccessStatusCode();
        var cleared = await GetApplicationAsync(applicationId);
        cleared.InterviewAt.ShouldBeNull();
        cleared.InterviewFormat.ShouldBeNull();
    }

    [Fact]
    public async Task An_Interview_Is_Refused_Outside_An_Interview_Stage()
    {
        var applicationId = await CreateApplicationInStageAsync("Applied Co", ApplicationStatus.Offer);

        var response = await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddDays(1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_Users_Application_Cannot_Be_Given_An_Interview()
    {
        var applicationId = await CreateApplicationInStageAsync("Mine Co");
        var other = await CreateAuthenticatedClientAsync("interviews.other@example.com");

        var response = await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddDays(1), client: other);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetApplicationAsync(applicationId)).InterviewAt.ShouldBeNull();
    }

    [Fact]
    public async Task Upcoming_Lists_The_Callers_Current_Stage_Interviews_Soonest_First()
    {
        var later = await CreateApplicationInStageAsync("Later Co");
        var sooner = await CreateApplicationInStageAsync("Sooner Co", ApplicationStatus.FinalInterview);
        var movedOn = await CreateApplicationInStageAsync("Moved Co");
        var farAway = await CreateApplicationInStageAsync("Far Co");
        (await PutInterviewAsync(later, DateTimeOffset.UtcNow.AddDays(5))).EnsureSuccessStatusCode();
        (await PutInterviewAsync(sooner, DateTimeOffset.UtcNow.AddDays(1), InterviewFormat.InPerson)).EnsureSuccessStatusCode();
        (await PutInterviewAsync(movedOn, DateTimeOffset.UtcNow.AddDays(2))).EnsureSuccessStatusCode();
        (await PutInterviewAsync(farAway, DateTimeOffset.UtcNow.AddDays(40))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/applications/{movedOn}/status",
            new ChangeStatusRequest(ApplicationStatus.Offer, null, null), JsonOptions)).EnsureSuccessStatusCode();

        var upcoming = await GetUpcomingAsync();

        upcoming.Select(u => u.ApplicationId).ShouldBe([sooner, later]);
        upcoming[0].CompanyName.ShouldBe("Sooner Co");
        upcoming[0].Status.ShouldBe(ApplicationStatus.FinalInterview);
        upcoming[0].Format.ShouldBe(InterviewFormat.InPerson);

        var other = await CreateAuthenticatedClientAsync("interviews.upcoming.other@example.com");
        (await GetUpcomingAsync(other)).ShouldBeEmpty();
    }

    [Fact]
    public async Task No_Question_Is_Asked_Before_The_Interview_Is_Over()
    {
        var applicationId = await CreateApplicationInStageAsync("Soon Co");
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddMinutes(-20))).EnsureSuccessStatusCode();

        await ScanAsync();

        (await GetRemindersAsync()).ShouldNotContain(r => r.Type == ReminderType.InterviewHeld);
    }

    [Fact]
    public async Task A_Held_Interview_Asks_How_It_Went_First_In_The_List_And_Retires_The_Follow_Up()
    {
        // Ten days in the stage: a follow-up is due before the interview is recorded.
        var applicationId = await CreateApplicationInStageAsync("Held Co", stageDaysAgo: 10);
        var stale = await CreateApplicationInStageAsync("Older Co", stageDaysAgo: 20);
        await ScanAsync();
        (await GetRemindersAsync()).ShouldContain(r => r.ApplicationId == applicationId && r.Type == ReminderType.FollowUp);

        var at = DateTimeOffset.UtcNow.AddHours(-3);
        (await PutInterviewAsync(applicationId, at)).EnsureSuccessStatusCode();
        await ScanAsync();

        var reminders = await GetRemindersAsync();
        var first = reminders[0];
        first.ApplicationId.ShouldBe(applicationId);
        first.Type.ShouldBe(ReminderType.InterviewHeld);
        first.ApplicationStatus.ShouldBe(ApplicationStatus.Interview);
        first.InterviewAt!.Value.ShouldBe(at, TimeSpan.FromMilliseconds(1));
        reminders.Count(r => r.ApplicationId == applicationId).ShouldBe(1);
        reminders.ShouldContain(r => r.ApplicationId == stale);
    }

    [Fact]
    public async Task A_Recorded_Reply_Date_Is_Already_An_Answer()
    {
        var applicationId = await CreateApplicationInStageAsync("Promise Co");
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddHours(-3))).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/applications/{applicationId}/reply-promise",
            new SetReplyPromiseRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)), JsonOptions)).EnsureSuccessStatusCode();

        await ScanAsync();

        (await GetRemindersAsync()).ShouldNotContain(r => r.ApplicationId == applicationId);
    }

    [Fact]
    public async Task Moving_The_Interview_Retires_The_Old_Question_And_Asks_About_The_New_One_Later()
    {
        var (applicationId, reminder) = await HeldInterviewAsync("Moved Date Co");

        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddDays(2))).EnsureSuccessStatusCode();
        await ScanAsync();

        (await GetRemindersAsync()).ShouldNotContain(r => r.ApplicationId == applicationId);
        (await GetReminderRowAsync(reminder.Id)).DismissedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Status_Changed_Elsewhere_Hides_The_Question_At_Once()
    {
        var (applicationId, _) = await HeldInterviewAsync("Elsewhere Co");

        (await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.FinalInterview, null, null), JsonOptions)).EnsureSuccessStatusCode();

        (await GetRemindersAsync()).ShouldNotContain(r => r.ApplicationId == applicationId);
    }

    [Fact]
    public async Task Next_Stage_Moves_The_Application_Closes_The_Row_And_Undo_Puts_Both_Back()
    {
        var (applicationId, reminder) = await HeldInterviewAsync("Next Co");

        var response = await AnswerAsync(reminder.Id,
            new InterviewOutcomeRequest(InterviewOutcome.NextStage, ApplicationStatus.FinalInterview));
        response.EnsureSuccessStatusCode();
        var outcome = (await response.Content.ReadFromJsonAsync<InterviewOutcomeResponse>(JsonOptions))!;

        outcome.FromStatus.ShouldBe(ApplicationStatus.Interview);
        outcome.ToStatus.ShouldBe(ApplicationStatus.FinalInterview);
        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.FinalInterview);
        (await GetRemindersAsync()).ShouldNotContain(r => r.Id == reminder.Id);

        var undo = await _client.PostAsJsonAsync($"/api/reminders/{reminder.Id}/interview-outcome/undo",
            new UndoInterviewOutcomeRequest(outcome.FromStatus, outcome.ToStatus, outcome.PromisedReplyBy), JsonOptions);
        undo.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.Interview);
        (await GetRemindersAsync()).ShouldContain(r => r.Id == reminder.Id);
    }

    [Fact]
    public async Task Waiting_With_A_Date_Records_The_Promise_And_Undo_Clears_It()
    {
        var (applicationId, reminder) = await HeldInterviewAsync("Wait Co");
        var promised = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

        var response = await AnswerAsync(reminder.Id, new InterviewOutcomeRequest(InterviewOutcome.Waiting, PromisedReplyBy: promised));
        response.EnsureSuccessStatusCode();
        var outcome = (await response.Content.ReadFromJsonAsync<InterviewOutcomeResponse>(JsonOptions))!;

        outcome.FromStatus.ShouldBeNull();
        outcome.PromisedReplyBy.ShouldBe(promised);
        var detail = await GetApplicationAsync(applicationId);
        detail.Status.ShouldBe(ApplicationStatus.Interview);
        detail.PromisedReplyBy.ShouldBe(promised);
        (await GetRemindersAsync()).ShouldNotContain(r => r.Id == reminder.Id);

        (await _client.PostAsJsonAsync($"/api/reminders/{reminder.Id}/interview-outcome/undo",
            new UndoInterviewOutcomeRequest(null, null, promised), JsonOptions)).EnsureSuccessStatusCode();

        (await GetApplicationAsync(applicationId)).PromisedReplyBy.ShouldBeNull();
        (await GetRemindersAsync()).ShouldContain(r => r.Id == reminder.Id);
    }

    [Fact]
    public async Task Waiting_Without_A_Date_Closes_The_Row_And_The_Scan_Does_Not_Ask_Again()
    {
        var (_, reminder) = await HeldInterviewAsync("Quiet Co");

        (await AnswerAsync(reminder.Id, new InterviewOutcomeRequest(InterviewOutcome.Waiting))).EnsureSuccessStatusCode();
        await ScanAsync();

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejected_Closes_The_Application_And_The_Row()
    {
        var (applicationId, reminder) = await HeldInterviewAsync("No Co");

        (await AnswerAsync(reminder.Id, new InterviewOutcomeRequest(InterviewOutcome.Rejected))).EnsureSuccessStatusCode();

        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.Rejected);
        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Only_An_Interview_Question_Takes_An_Interview_Answer()
    {
        var applicationId = await CreateApplicationInStageAsync("Follow Co", stageDaysAgo: 10);
        await ScanAsync();
        var followUp = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);
        followUp.Type.ShouldBe(ReminderType.FollowUp);

        var response = await AnswerAsync(followUp.Id, new InterviewOutcomeRequest(InterviewOutcome.Rejected));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.Interview);
    }

    [Fact]
    public async Task Another_User_Can_Neither_Answer_Nor_Undo_The_Question()
    {
        var (applicationId, reminder) = await HeldInterviewAsync("Private Co");
        var other = await CreateAuthenticatedClientAsync("interviews.intruder@example.com");

        (await AnswerAsync(reminder.Id, new InterviewOutcomeRequest(InterviewOutcome.Rejected), other))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsJsonAsync($"/api/reminders/{reminder.Id}/interview-outcome/undo",
                new UndoInterviewOutcomeRequest(ApplicationStatus.Applied, ApplicationStatus.Interview, null), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await GetApplicationAsync(applicationId)).Status.ShouldBe(ApplicationStatus.Interview);
    }

    [Fact]
    public async Task A_Snoozed_Reminder_Leaves_The_List_Survives_The_Scan_And_Comes_Back_On_Undo()
    {
        var applicationId = await CreateApplicationInStageAsync("Snooze Co", stageDaysAgo: 10);
        await ScanAsync();
        var reminder = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);

        (await _client.PostAsJsonAsync($"/api/reminders/{reminder.Id}/snooze", new SnoozeReminderRequest(7), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetRemindersAsync()).ShouldBeEmpty();

        var row = await GetReminderRowAsync(reminder.Id);
        row.SnoozedUntil!.Value.ShouldBe(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        await ScanAsync();
        (await GetRemindersAsync()).ShouldBeEmpty();

        (await _client.PostAsync($"/api/reminders/{reminder.Id}/unsnooze", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetRemindersAsync()).ShouldHaveSingleItem().Id.ShouldBe(reminder.Id);
    }

    [Fact]
    public async Task A_Snooze_That_Has_Run_Out_Puts_The_Row_Back()
    {
        var applicationId = await CreateApplicationInStageAsync("Woke Co", stageDaysAgo: 10);
        await ScanAsync();
        var reminder = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);

        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Reminders.Where(r => r.Id == reminder.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SnoozedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        await host.ClearCachesAsync();
        (await GetRemindersAsync()).ShouldHaveSingleItem().Id.ShouldBe(reminder.Id);
    }

    [Fact]
    public async Task Snooze_Refuses_Other_Lengths_And_Other_Users()
    {
        var applicationId = await CreateApplicationInStageAsync("Length Co", stageDaysAgo: 10);
        await ScanAsync();
        var reminder = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);
        var other = await CreateAuthenticatedClientAsync("interviews.snoozer@example.com");

        (await _client.PostAsJsonAsync($"/api/reminders/{reminder.Id}/snooze", new SnoozeReminderRequest(30), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await other.PostAsJsonAsync($"/api/reminders/{reminder.Id}/snooze", new SnoozeReminderRequest(7), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/reminders/{reminder.Id}/unsnooze", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await GetRemindersAsync()).ShouldHaveSingleItem();
    }

    // --- The bell -------------------------------------------------------------------------------

    private async Task<List<NotificationFeedItemResponse>> GetFeedAsync(HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync("/api/notifications?page=1&pageSize=20");
        response.EnsureSuccessStatusCode();
        return [.. (await response.Content.ReadFromJsonAsync<PagedResult<NotificationFeedItemResponse>>(JsonOptions))!.Items];
    }

    private async Task<int> GetUnreadCountAsync()
    {
        var response = await _client.GetAsync("/api/notifications/count");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotificationFeedCountResponse>(JsonOptions))!.UnreadCount;
    }

    [Fact]
    public async Task An_Interview_Tomorrow_Rings_The_Bell_Once_And_Clears_Like_Any_Row()
    {
        var applicationId = await CreateApplicationInStageAsync("Bell Co", ApplicationStatus.TechnicalInterview);
        var at = DateTimeOffset.UtcNow.AddHours(20);
        (await PutInterviewAsync(applicationId, at)).EnsureSuccessStatusCode();

        await ScanAsync();
        await ScanAsync();

        var row = (await GetFeedAsync()).ShouldHaveSingleItem();
        row.Kind.ShouldBe(NotificationFeedKind.Interview);
        row.IsRead.ShouldBeFalse();
        row.Interview!.Kind.ShouldBe(InterviewNotificationKind.Upcoming);
        row.Interview.ApplicationId.ShouldBe(applicationId);
        row.Interview.CompanyName.ShouldBe("Bell Co");
        row.Interview.Status.ShouldBe(ApplicationStatus.TechnicalInterview);
        (await GetUnreadCountAsync()).ShouldBe(1);

        (await _client.PostAsync("/api/notifications/read", null)).EnsureSuccessStatusCode();
        (await GetUnreadCountAsync()).ShouldBe(0);

        (await _client.PostAsync($"/api/notifications/{row.Id}/dismiss", null)).EnsureSuccessStatusCode();
        (await GetFeedAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_Interview_Further_Out_Does_Not_Ring_Yet()
    {
        var applicationId = await CreateApplicationInStageAsync("Later Bell Co");
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddDays(4))).EnsureSuccessStatusCode();

        await ScanAsync();

        (await GetFeedAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Moved_Interview_Takes_Its_Old_Bell_Row_With_It()
    {
        var applicationId = await CreateApplicationInStageAsync("Moved Bell Co");
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddHours(20))).EnsureSuccessStatusCode();
        await ScanAsync();
        (await GetFeedAsync()).ShouldHaveSingleItem();

        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddDays(5))).EnsureSuccessStatusCode();

        (await GetFeedAsync()).ShouldBeEmpty();
        (await GetUnreadCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task How_Did_It_Go_Rings_The_Bell_Until_It_Is_Answered()
    {
        var (_, reminder) = await HeldInterviewAsync("Asked Co");

        var row = (await GetFeedAsync()).ShouldHaveSingleItem();
        row.Interview!.Kind.ShouldBe(InterviewNotificationKind.Held);

        (await AnswerAsync(reminder.Id, new InterviewOutcomeRequest(InterviewOutcome.Waiting))).EnsureSuccessStatusCode();

        (await GetFeedAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Interview_Notifications_Off_Writes_None_And_An_Older_Client_Leaves_The_Switch_Alone()
    {
        var preferences = await _client.GetFromJsonAsync<NotificationPreferencesResponse>(
            "/api/users/me/notification-preferences", JsonOptions);
        preferences!.Interviews.ShouldBeTrue();

        (await _client.PutAsJsonAsync("/api/users/me/notification-preferences",
            new UpdateNotificationPreferencesRequest(true, true, true, true, true, true, Interviews: false), JsonOptions))
            .EnsureSuccessStatusCode();

        // A build that predates the switch sends the six old fields only.
        var old = await _client.PutAsJsonAsync("/api/users/me/notification-preferences",
            new { contributions = true, reviewHelpful = true, salaryHelpful = true, experienceHelpful = true,
                blogCommentHelpful = true, gmailUpdates = true }, JsonOptions);
        old.EnsureSuccessStatusCode();
        (await old.Content.ReadFromJsonAsync<NotificationPreferencesResponse>(JsonOptions))!.Interviews.ShouldBeFalse();

        var applicationId = await CreateApplicationInStageAsync("Quiet Bell Co");
        (await PutInterviewAsync(applicationId, DateTimeOffset.UtcNow.AddHours(20))).EnsureSuccessStatusCode();
        await ScanAsync();

        (await GetFeedAsync()).ShouldBeEmpty();
        using var scope = host.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().InterviewNotifications.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Another_Users_Interview_Never_Reaches_This_Bell()
    {
        var other = await CreateAuthenticatedClientAsync("interviews.bell.other@example.com");
        var theirs = await CreateApplicationInStageAsync("Their Co", client: other);
        (await PutInterviewAsync(theirs, DateTimeOffset.UtcNow.AddHours(20), client: other)).EnsureSuccessStatusCode();

        await ScanAsync();

        (await GetFeedAsync()).ShouldBeEmpty();
        (await GetFeedAsync(other)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_Export_Carries_The_Interview_The_Snooze_And_The_Bell_Rows()
    {
        var applicationId = await CreateApplicationInStageAsync("Export Co", stageDaysAgo: 10);
        var at = DateTimeOffset.UtcNow.AddHours(20);
        (await PutInterviewAsync(applicationId, at, InterviewFormat.Phone)).EnsureSuccessStatusCode();
        await ScanAsync();
        var followUp = (await GetRemindersAsync()).Single(r => r.ApplicationId == applicationId);
        (await _client.PostAsJsonAsync($"/api/reminders/{followUp.Id}/snooze", new SnoozeReminderRequest(3), JsonOptions))
            .EnsureSuccessStatusCode();

        var export = await _client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions);

        var application = export!.Applications.Single(a => a.Id == applicationId);
        application.InterviewAt!.Value.ShouldBe(at, TimeSpan.FromMilliseconds(1));
        application.InterviewFormat.ShouldBe(InterviewFormat.Phone);
        application.InterviewStatus.ShouldBe(ApplicationStatus.Interview);
        export.Reminders.Single(r => r.Id == followUp.Id).SnoozedUntil.ShouldNotBeNull();
        export.InterviewNotifications!.ShouldHaveSingleItem().Kind.ShouldBe(InterviewNotificationKind.Upcoming);
    }
}
