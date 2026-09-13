using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
public class ReminderTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(ReminderTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });


        _client = await CreateAuthenticatedClientAsync("reminders.test@example.com");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

    }

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

    private async Task<List<ReminderResponse>> GetRemindersAsync(HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync("/api/reminders");
        response.EnsureSuccessStatusCode();
        var reminders = await response.Content.ReadFromJsonAsync<List<ReminderResponse>>(JsonOptions);
        return reminders!;
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
}
