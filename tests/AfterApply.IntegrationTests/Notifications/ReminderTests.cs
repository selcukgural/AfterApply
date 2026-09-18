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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Notifications;

[Collection(IntegrationTestCollection.Name)]
public class ReminderTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = await CreateAuthenticatedClientAsync("reminders.test@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Reminders", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string companyName, DateTimeOffset appliedAt)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private static async Task ChangeStatusAsync(HttpClient client, Guid applicationId, ApplicationStatus status, DateTimeOffset changedAt)
    {
        var response = await client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, changedAt), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> ScanAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
        return await reminderService.ScanAndGenerateRemindersAsync(CancellationToken.None);
    }

    private async Task<PagedResult<ReminderResponse>> GetRemindersPageAsync(int page = 1, int pageSize = 50, HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync($"/api/reminders?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var reminders = await response.Content.ReadFromJsonAsync<PagedResult<ReminderResponse>>(JsonOptions);
        return reminders!;
    }

    /// <summary>The whole list, for the tests that care about what is in it rather than how it pages.</summary>
    private async Task<List<ReminderResponse>> GetRemindersAsync(HttpClient? client = null)
    {
        var page = await GetRemindersPageAsync(client: client);
        page.TotalCount.ShouldBe(page.Items.Count, "these tests never create more than one page");
        return [.. page.Items];
    }

    private async Task<HttpResponseMessage> BulkAsync(string action, BulkReminderRequest request, HttpClient? client = null) =>
        await (client ?? _client).PostAsJsonAsync($"/api/reminders/bulk/{action}", request, JsonOptions);

    private static async Task<BulkReminderResponse> ReadBulkAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BulkReminderResponse>(JsonOptions))!;
    }

    private async Task<ApplicationStatus> GetStatusAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Status;
    }

    private async Task<int> CountFollowUpEventsAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}/timeline");
        response.EnsureSuccessStatusCode();
        var timeline = await response.Content.ReadFromJsonAsync<List<ApplicationEventResponse>>(JsonOptions);
        return timeline!.Count(e => e.Type == ApplicationEventType.FollowUpSent);
    }

    [Fact]
    public async Task Scan_Creates_FollowUp_Reminder_For_Stale_NonTerminal_Application()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var applicationId = await CreateApplicationAsync(_client, "Stale Co", appliedAt);

        await ScanAsync();

        var reminders = await GetRemindersAsync();

        var reminder = reminders.ShouldHaveSingleItem();
        reminder.ApplicationId.ShouldBe(applicationId);
        reminder.Type.ShouldBe(ReminderType.FollowUp);
        reminder.CompanyName.ShouldBe("Stale Co");
        reminder.DaysElapsed.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task Scan_Does_Not_Create_Reminder_For_Application_Within_Threshold()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-2);
        await CreateApplicationAsync(_client, "Fresh Co", appliedAt);

        await ScanAsync();

        var reminders = await GetRemindersAsync();

        reminders.ShouldBeEmpty();
    }

    [Fact]
    public async Task Scan_Does_Not_Create_PossiblyGhosted_For_Application_That_Responded()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);
        var applicationId = await CreateApplicationAsync(_client, "Responded Co", appliedAt);
        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Screening, appliedAt.AddDays(3));

        await ScanAsync();

        var reminders = await GetRemindersAsync();

        reminders.ShouldAllBe(r => r.Type != ReminderType.PossiblyGhosted);
    }

    [Fact]
    public async Task Scan_Creates_Only_PossiblyGhosted_Not_FollowUp_When_Both_Would_Apply()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);
        var applicationId = await CreateApplicationAsync(_client, "Ghosted Co", appliedAt);

        await ScanAsync();

        var reminders = await GetRemindersAsync();

        var reminder = reminders.ShouldHaveSingleItem();
        reminder.ApplicationId.ShouldBe(applicationId);
        reminder.Type.ShouldBe(ReminderType.PossiblyGhosted);
    }

    [Fact]
    public async Task PossiblyGhosted_Row_Carries_The_Users_Own_Median_Reply_Time()
    {
        // Three answered applications (the floor) at 2, 9 and 40 days: the median is 9, and the
        // one slow reply does not drag it — that number is the sentence's whole point.
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-60);
        foreach (var (name, days) in new[] { ("Fast Co", 2), ("Usual Co", 9), ("Slow Co", 40) })
        {
            var id = await CreateApplicationAsync(_client, name, appliedAt);
            await ChangeStatusAsync(_client, id, ApplicationStatus.Screening, appliedAt.AddDays(days));
        }

        var ghosted = await CreateApplicationAsync(_client, "Silent Co", DateTimeOffset.UtcNow.AddDays(-40));

        await ScanAsync();

        // The answered applications are still open and old enough to earn follow-up reminders of
        // their own; the row under test is the one about the silent application.
        var reminder = (await GetRemindersAsync()).Single(r => r.Type == ReminderType.PossiblyGhosted);
        reminder.ApplicationId.ShouldBe(ghosted);
        reminder.UserMedianResponseDays.ShouldBe(9);
    }

    [Fact]
    public async Task PossiblyGhosted_Row_Has_No_Median_Below_The_Minimum_Sample()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-60);
        foreach (var (name, days) in new[] { ("Fast Co", 2), ("Usual Co", 9) })
        {
            var id = await CreateApplicationAsync(_client, name, appliedAt);
            await ChangeStatusAsync(_client, id, ApplicationStatus.Screening, appliedAt.AddDays(days));
        }

        await CreateApplicationAsync(_client, "Silent Co", DateTimeOffset.UtcNow.AddDays(-40));

        await ScanAsync();

        var reminder = (await GetRemindersAsync()).Single(r => r.Type == ReminderType.PossiblyGhosted);
        reminder.UserMedianResponseDays.ShouldBeNull();
    }

    [Fact]
    public async Task Median_Only_Counts_The_Callers_Own_Applications()
    {
        // Another user's fast replies must not become this user's norm.
        var other = await CreateAuthenticatedClientAsync("reminders.other@example.com");
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-60);
        foreach (var name in new[] { "A Co", "B Co", "C Co" })
        {
            var id = await CreateApplicationAsync(other, name, appliedAt);
            await ChangeStatusAsync(other, id, ApplicationStatus.Screening, appliedAt.AddDays(1));
        }

        await CreateApplicationAsync(_client, "Silent Co", DateTimeOffset.UtcNow.AddDays(-40));

        await ScanAsync();

        var reminder = (await GetRemindersAsync()).ShouldHaveSingleItem();
        reminder.UserMedianResponseDays.ShouldBeNull();
    }

    [Fact]
    public async Task Scan_Run_Twice_Does_Not_Create_Duplicate_Reminders()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await CreateApplicationAsync(_client, "Stale Co", appliedAt);

        var firstScanCount = await ScanAsync();
        var secondScanCount = await ScanAsync();

        firstScanCount.ShouldBe(1);
        secondScanCount.ShouldBe(0);

        var reminders = await GetRemindersAsync();
        reminders.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Dismiss_Removes_Reminder_And_Rescan_Does_Not_Resurrect_It()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await CreateApplicationAsync(_client, "Stale Co", appliedAt);
        await ScanAsync();

        var reminder = (await GetRemindersAsync()).ShouldHaveSingleItem();

        var dismissResponse = await _client.PostAsync($"/api/reminders/{reminder.Id}/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetRemindersAsync()).ShouldBeEmpty();

        await ScanAsync();

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dismiss_Unknown_Reminder_Returns_NotFound()
    {
        var response = await _client.PostAsync($"/api/reminders/{Guid.NewGuid()}/dismiss", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_Cannot_See_Or_Dismiss_Another_Users_Reminder()
    {
        var otherClient = await CreateAuthenticatedClientAsync("reminders.other@example.com");

        var appliedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await CreateApplicationAsync(_client, "Stale Co", appliedAt);
        await ScanAsync();

        var myReminder = (await GetRemindersAsync()).ShouldHaveSingleItem();

        (await GetRemindersAsync(otherClient)).ShouldBeEmpty();

        var dismissResponse = await otherClient.PostAsync($"/api/reminders/{myReminder.Id}/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await GetRemindersAsync()).ShouldHaveSingleItem();
    }

    private async Task<Guid> InsertReminderAsync(Guid applicationId, ReminderType type, DateTimeOffset referenceAt)
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await dbContext.Applications.Where(a => a.Id == applicationId).Select(a => a.UserId).SingleAsync();
        var reminder = Reminder.Create(userId, applicationId, type, referenceAt, 10, DateTimeOffset.UtcNow);
        dbContext.Reminders.Add(reminder);
        await dbContext.SaveChangesAsync();
        return reminder.Id;
    }

    private async Task<bool> IsDismissedAsync(Guid reminderId)
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Reminders.Where(r => r.Id == reminderId).Select(r => r.DismissedAt != null).SingleAsync();
    }

    [Fact]
    public async Task Scan_Creates_Nothing_For_Application_Beyond_The_Stale_Horizon()
    {
        // A 2017 LinkedIn import is the case: 3,000-odd days is not a reminder, it is the stale
        // batch the dashboard asks about in one question.
        await CreateApplicationAsync(_client, "Ancient Co", DateTimeOffset.UtcNow.AddDays(-3518));

        (await ScanAsync()).ShouldBe(0);

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scan_Retires_Reminder_Whose_Application_Went_Beyond_The_Horizon()
    {
        var applicationId = await CreateApplicationAsync(_client, "Old Co", DateTimeOffset.UtcNow.AddDays(-200));
        var reminderId = await InsertReminderAsync(applicationId, ReminderType.PossiblyGhosted, DateTimeOffset.UtcNow.AddDays(-200));

        await ScanAsync();

        (await IsDismissedAsync(reminderId)).ShouldBeTrue();
        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scan_Retires_Reminder_Whose_Application_Is_Terminal()
    {
        var applicationId = await CreateApplicationAsync(_client, "Closed Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Withdrawn, DateTimeOffset.UtcNow);
        // Inserted after the status change so the change itself cannot have closed it: this is the
        // scan's sweep under test, not ApplicationService's.
        var reminderId = await InsertReminderAsync(applicationId, ReminderType.FollowUp, DateTimeOffset.UtcNow.AddDays(-10));

        await ScanAsync();

        (await IsDismissedAsync(reminderId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Scan_Retires_FollowUp_When_PossiblyGhosted_Supersedes_It()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddDays(-40);
        var applicationId = await CreateApplicationAsync(_client, "Superseded Co", appliedAt);
        // A follow-up from an earlier reference point — what the scan would have made at day 7 had
        // it run then.
        var followUpId = await InsertReminderAsync(applicationId, ReminderType.FollowUp, appliedAt.AddDays(-1));

        await ScanAsync();

        (await IsDismissedAsync(followUpId)).ShouldBeTrue();
        var reminder = (await GetRemindersAsync()).ShouldHaveSingleItem();
        reminder.Type.ShouldBe(ReminderType.PossiblyGhosted);
    }

    [Fact]
    public async Task Changing_Status_To_Terminal_Retires_The_Reminder_At_Once()
    {
        var applicationId = await CreateApplicationAsync(_client, "Ghosted Now Co", DateTimeOffset.UtcNow.AddDays(-40));
        await ScanAsync();
        (await GetRemindersAsync()).ShouldHaveSingleItem();

        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Ghosted, DateTimeOffset.UtcNow);

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_Status_To_NonTerminal_Keeps_The_Reminder()
    {
        var applicationId = await CreateApplicationAsync(_client, "Still Open Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();

        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Screening, DateTimeOffset.UtcNow);

        (await GetRemindersAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Bulk_Status_Change_To_Terminal_Retires_Reminders()
    {
        var first = await CreateApplicationAsync(_client, "Bulk A", DateTimeOffset.UtcNow.AddDays(-10));
        var second = await CreateApplicationAsync(_client, "Bulk B", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        (await GetRemindersAsync()).Count.ShouldBe(2);

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [first, second]), ApplicationStatus.Rejected), JsonOptions);
        response.EnsureSuccessStatusCode();

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Active_List_Hides_Reminders_Of_Terminal_Applications_Even_When_Still_Open()
    {
        var applicationId = await CreateApplicationAsync(_client, "Hidden Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Rejected, DateTimeOffset.UtcNow);
        await InsertReminderAsync(applicationId, ReminderType.FollowUp, DateTimeOffset.UtcNow.AddDays(-10));

        (await GetRemindersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task FollowUp_Records_Event_And_Closes_The_Reminder()
    {
        var applicationId = await CreateApplicationAsync(_client, "Followed Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        var reminder = (await GetRemindersAsync()).ShouldHaveSingleItem();

        var response = await _client.PostAsync($"/api/reminders/{reminder.Id}/follow-up", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetRemindersAsync()).ShouldBeEmpty();

        var timelineResponse = await _client.GetAsync($"/api/applications/{applicationId}/timeline");
        timelineResponse.EnsureSuccessStatusCode();
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<ApplicationEventResponse>>(JsonOptions);
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.FollowUpSent);

        // Already closed: a second answer has nothing to act on.
        (await _client.PostAsync($"/api/reminders/{reminder.Id}/follow-up", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FollowUp_On_Another_Users_Reminder_Is_NotFound_And_Changes_Nothing()
    {
        var otherClient = await CreateAuthenticatedClientAsync("reminders.followup.other@example.com");
        await CreateApplicationAsync(_client, "Mine Co", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        var mine = (await GetRemindersAsync()).ShouldHaveSingleItem();

        var response = await otherClient.PostAsync($"/api/reminders/{mine.Id}/follow-up", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetRemindersAsync()).ShouldHaveSingleItem();
    }

    // --- Paging -----------------------------------------------------------------------------------

    [Fact]
    public async Task List_Is_Paged_Longest_Waiting_First()
    {
        // Seven reminders, five to a page: the dashboard card's own numbers. Days elapsed differ so
        // the order is checkable — the application that has waited longest leads.
        var ids = new List<Guid>();
        for (var day = 10; day < 17; day++)
        {
            ids.Add(await CreateApplicationAsync(_client, $"Paged {day}", DateTimeOffset.UtcNow.AddDays(-day)));
        }
        await ScanAsync();

        var first = await GetRemindersPageAsync(page: 1, pageSize: 5);
        first.TotalCount.ShouldBe(7);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(5);
        first.Items.Count.ShouldBe(5);
        first.Items.Select(r => r.DaysElapsed).ShouldBe(first.Items.Select(r => r.DaysElapsed).OrderByDescending(d => d));
        first.Items.First().ApplicationId.ShouldBe(ids.Last());

        var second = await GetRemindersPageAsync(page: 2, pageSize: 5);
        second.TotalCount.ShouldBe(7);
        second.Items.Count.ShouldBe(2);
        second.Items.Select(r => r.Id).ShouldNotContain(id => first.Items.Select(f => f.Id).Contains(id));

        // Past the end: an empty page, the same total — a client that lands here after answering
        // the last row of the last page clamps from TotalCount, not from an error.
        var third = await GetRemindersPageAsync(page: 3, pageSize: 5);
        third.Items.ShouldBeEmpty();
        third.TotalCount.ShouldBe(7);
    }

    [Fact]
    public async Task List_Rejects_A_Page_Size_Beyond_The_Ceiling()
    {
        (await _client.GetAsync("/api/reminders?page=1&pageSize=51")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAsync("/api/reminders?page=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Answering_A_Reminder_Refreshes_Every_Page()
    {
        // The list is cached per page under one tag; a dismiss must drop the whole list, not just
        // the page the row was on, or page two keeps reporting a total that page one has moved past.
        for (var day = 10; day < 16; day++)
        {
            await CreateApplicationAsync(_client, $"Cached {day}", DateTimeOffset.UtcNow.AddDays(-day));
        }
        await ScanAsync();
        var firstPage = await GetRemindersPageAsync(page: 1, pageSize: 5);
        (await GetRemindersPageAsync(page: 2, pageSize: 5)).TotalCount.ShouldBe(6);

        var response = await _client.PostAsync($"/api/reminders/{firstPage.Items.First().Id}/dismiss", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetRemindersPageAsync(page: 2, pageSize: 5)).TotalCount.ShouldBe(5);
        (await GetRemindersPageAsync(page: 1, pageSize: 5)).Items.Count.ShouldBe(5);
    }

    // --- Bulk answers -----------------------------------------------------------------------------

    [Fact]
    public async Task Bulk_Dismiss_By_Ids_Closes_Only_Those_And_Ignores_What_Is_Not_The_Callers()
    {
        var otherClient = await CreateAuthenticatedClientAsync("reminders.bulk.other@example.com");
        await CreateApplicationAsync(otherClient, "Theirs", DateTimeOffset.UtcNow.AddDays(-10));
        await CreateApplicationAsync(_client, "Mine A", DateTimeOffset.UtcNow.AddDays(-10));
        await CreateApplicationAsync(_client, "Mine B", DateTimeOffset.UtcNow.AddDays(-10));
        await CreateApplicationAsync(_client, "Mine C", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        var mine = await GetRemindersAsync();
        var theirs = (await GetRemindersAsync(otherClient)).ShouldHaveSingleItem();

        var result = await ReadBulkAsync(await BulkAsync("dismiss", new BulkReminderRequest(
            new ReminderSelection(Ids: [mine[0].Id, mine[1].Id, theirs.Id, Guid.NewGuid()]))));

        result.Affected.ShouldBe(2);
        (await GetRemindersAsync()).ShouldHaveSingleItem().Id.ShouldBe(mine[2].Id);
        (await GetRemindersAsync(otherClient)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Bulk_Dismiss_All_Closes_Every_Reminder_When_The_Count_Holds()
    {
        for (var day = 10; day < 17; day++)
        {
            await CreateApplicationAsync(_client, $"All {day}", DateTimeOffset.UtcNow.AddDays(-day));
        }
        await ScanAsync();

        var result = await ReadBulkAsync(await BulkAsync("dismiss", new BulkReminderRequest(
            new ReminderSelection(All: true), ExpectedCount: 7)));

        result.Affected.ShouldBe(7);
        (await GetRemindersPageAsync()).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Bulk_All_With_A_Stale_Count_Is_Refused_And_Changes_Nothing()
    {
        await CreateApplicationAsync(_client, "Stale Count A", DateTimeOffset.UtcNow.AddDays(-10));
        await CreateApplicationAsync(_client, "Stale Count B", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();

        foreach (var action in new[] { "dismiss", "follow-up", "ghost" })
        {
            var response = await BulkAsync(action, new BulkReminderRequest(new ReminderSelection(All: true), ExpectedCount: 1));

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict, action);
            var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(JsonOptions);
            problem!["errorCode"].GetString().ShouldBe("BULK_COUNT_MISMATCH");
            problem["actualCount"].GetInt32().ShouldBe(2);
            problem["expectedCount"].GetInt32().ShouldBe(1);
        }

        (await GetRemindersAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Bulk_All_Without_A_Count_Is_A_Validation_Error()
    {
        var response = await BulkAsync("dismiss", new BulkReminderRequest(new ReminderSelection(All: true)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bulk_Selection_Must_Be_Exactly_One_Form()
    {
        var both = await BulkAsync("dismiss", new BulkReminderRequest(new ReminderSelection(Ids: [Guid.NewGuid()], All: true), 1));
        var neither = await BulkAsync("dismiss", new BulkReminderRequest(new ReminderSelection()));
        var empty = await BulkAsync("dismiss", new BulkReminderRequest(new ReminderSelection(Ids: [])));

        both.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        neither.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bulk_FollowUp_Records_One_Event_Per_Application_And_Closes_The_Reminders()
    {
        var first = await CreateApplicationAsync(_client, "Follow A", DateTimeOffset.UtcNow.AddDays(-10));
        var second = await CreateApplicationAsync(_client, "Follow B", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        // A second, older reminder on the same application: the follow-up is one act, not two.
        await InsertReminderAsync(first, ReminderType.PossiblyGhosted, DateTimeOffset.UtcNow.AddDays(-40));
        (await GetRemindersAsync()).Count.ShouldBe(3);

        var result = await ReadBulkAsync(await BulkAsync("follow-up", new BulkReminderRequest(
            new ReminderSelection(All: true), ExpectedCount: 3)));

        result.Affected.ShouldBe(3);
        (await GetRemindersAsync()).ShouldBeEmpty();
        (await CountFollowUpEventsAsync(first)).ShouldBe(1);
        (await CountFollowUpEventsAsync(second)).ShouldBe(1);
    }

    [Fact]
    public async Task Bulk_Ghost_Moves_The_Applications_Closes_The_Reminders_And_Can_Be_Undone()
    {
        var first = await CreateApplicationAsync(_client, "Ghost A", DateTimeOffset.UtcNow.AddDays(-10));
        var second = await CreateApplicationAsync(_client, "Ghost B", DateTimeOffset.UtcNow.AddDays(-10));
        var untouched = await CreateApplicationAsync(_client, "Ghost C", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        var reminders = await GetRemindersAsync();
        var selected = reminders.Where(r => r.ApplicationId != untouched).Select(r => r.Id).ToList();

        var response = await BulkAsync("ghost", new BulkReminderRequest(new ReminderSelection(Ids: selected)));
        response.EnsureSuccessStatusCode();
        var changed = (await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions))!;

        changed.Updated.ShouldBe(2);
        changed.Changes.Select(c => c.ApplicationId).ShouldBe([first, second], ignoreOrder: true);
        changed.Changes.ShouldAllBe(c => c.FromStatus == ApplicationStatus.Applied && c.ToStatus == ApplicationStatus.Ghosted);
        (await GetStatusAsync(first)).ShouldBe(ApplicationStatus.Ghosted);
        (await GetStatusAsync(untouched)).ShouldBe(ApplicationStatus.Applied);
        (await GetRemindersAsync()).ShouldHaveSingleItem().ApplicationId.ShouldBe(untouched);

        var undoResponse = await _client.PostAsJsonAsync("/api/reminders/bulk/ghost/undo", new UndoBulkStatusRequest(
            changed.Changes.Select(c => new UndoBulkStatusEntry(c.ApplicationId, c.ToStatus, c.FromStatus)).ToList()), JsonOptions);
        undoResponse.EnsureSuccessStatusCode();
        var undone = (await undoResponse.Content.ReadFromJsonAsync<UndoBulkStatusResponse>(JsonOptions))!;

        undone.Reverted.ShouldBe(2);
        (await GetStatusAsync(first)).ShouldBe(ApplicationStatus.Applied);
        (await GetStatusAsync(second)).ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Bulk_Ghost_Skips_An_Application_Already_Terminal()
    {
        // A reminder can outlive its application's terminal status until the nightly sweep; ghosting
        // through it must not report a change it did not make, nor offer an undo for one.
        var applicationId = await CreateApplicationAsync(_client, "Already Done", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();
        await ChangeStatusAsync(_client, applicationId, ApplicationStatus.Rejected, DateTimeOffset.UtcNow);
        var reminderId = await InsertReminderAsync(applicationId, ReminderType.FollowUp, DateTimeOffset.UtcNow.AddDays(-10));

        var response = await BulkAsync("ghost", new BulkReminderRequest(new ReminderSelection(Ids: [reminderId])));
        response.EnsureSuccessStatusCode();
        var changed = (await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions))!;

        changed.Updated.ShouldBe(0);
        changed.Changes.ShouldBeEmpty();
        (await GetStatusAsync(applicationId)).ShouldBe(ApplicationStatus.Rejected);
    }

    [Fact]
    public async Task Bulk_Answers_Never_Reach_Another_Users_Rows_Through_All()
    {
        var otherClient = await CreateAuthenticatedClientAsync("reminders.bulk.all.other@example.com");
        await CreateApplicationAsync(otherClient, "Theirs", DateTimeOffset.UtcNow.AddDays(-10));
        await CreateApplicationAsync(_client, "Mine", DateTimeOffset.UtcNow.AddDays(-10));
        await ScanAsync();

        var result = await ReadBulkAsync(await BulkAsync("dismiss", new BulkReminderRequest(
            new ReminderSelection(All: true), ExpectedCount: 1)));

        result.Affected.ShouldBe(1);
        (await GetRemindersAsync()).ShouldBeEmpty();
        (await GetRemindersAsync(otherClient)).ShouldHaveSingleItem();
    }
}
