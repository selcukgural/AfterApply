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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

/// <summary>
/// The dashboard's one question about an old import: "these N applications are past the horizon
/// and unanswered — mark them all as ghosted?" (DECISIONS.md 2026-09-13, panel reminders).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StaleApplicationsTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = await SignUpAsync("stale.owner@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> SignUpAsync(string email)
    {
        var client = _factory!.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory!.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Stale", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string companyName, int appliedDaysAgo)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-appliedDaysAgo), null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status, DateTimeOffset? changedAt = null)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, changedAt), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ApplicationStatus> GetStatusAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Status;
    }

    private async Task<StaleApplicationsSummaryResponse> GetSummaryAsync(HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync("/api/applications/stale");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StaleApplicationsSummaryResponse>(JsonOptions))!;
    }

    private async Task<BulkChangeStatusResponse> GhostAsync()
    {
        var response = await _client.PostAsync("/api/applications/stale/ghost", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Summary_Counts_Only_Applied_Rows_Past_The_Horizon_Without_A_Recent_Transition()
    {
        await CreateApplicationAsync(_client, "Ancient Co", 3518);
        await CreateApplicationAsync(_client, "Old Co", 120);
        await CreateApplicationAsync(_client, "Fresh Co", 10);
        var respondedOld = await CreateApplicationAsync(_client, "Screened Old Co", 120);
        await ChangeStatusAsync(respondedOld, ApplicationStatus.Screening);
        var closedOld = await CreateApplicationAsync(_client, "Rejected Old Co", 120);
        await ChangeStatusAsync(closedOld, ApplicationStatus.Rejected);
        // Bounced back to Applied recently: the recent real transition makes it current, not stale.
        var reopened = await CreateApplicationAsync(_client, "Reopened Co", 120);
        await ChangeStatusAsync(reopened, ApplicationStatus.Screening, DateTimeOffset.UtcNow.AddDays(-100));
        await ChangeStatusAsync(reopened, ApplicationStatus.Applied, DateTimeOffset.UtcNow.AddDays(-5));

        var summary = await GetSummaryAsync();

        summary.Count.ShouldBe(2);
        summary.OldestDays.ShouldBeGreaterThanOrEqualTo(3518);
        summary.ThresholdDays.ShouldBe(90);
        summary.Suggest.ShouldBeTrue();
    }

    [Fact]
    public async Task Summary_With_Nothing_Stale_Does_Not_Suggest()
    {
        await CreateApplicationAsync(_client, "Fresh Co", 10);

        var summary = await GetSummaryAsync();

        summary.Count.ShouldBe(0);
        summary.Suggest.ShouldBeFalse();
    }

    [Fact]
    public async Task Ghost_Moves_Every_Stale_Row_And_Nothing_Else()
    {
        var stale = await CreateApplicationAsync(_client, "Old Co", 120);
        var fresh = await CreateApplicationAsync(_client, "Fresh Co", 10);

        var result = await GhostAsync();

        result.Updated.ShouldBe(1);
        result.Changes.ShouldHaveSingleItem().ApplicationId.ShouldBe(stale);
        (await GetStatusAsync(stale)).ShouldBe(ApplicationStatus.Ghosted);
        (await GetStatusAsync(fresh)).ShouldBe(ApplicationStatus.Applied);
        (await GetSummaryAsync()).Count.ShouldBe(0);
    }

    [Fact]
    public async Task Ghost_Is_Not_Subject_To_The_Bulk_Ceiling()
    {
        // ApplicationBulkOptions.MaxOperationSize is 500; the question exists for imports far past it.
        using var scope = _factory!.Services.CreateScope();
        var ceiling = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AfterApply.Infrastructure.Applications.ApplicationBulkOptions>>()
            .Value.MaxOperationSize;
        var count = ceiling + 5;

        var tasks = Enumerable.Range(0, count).Select(i => CreateApplicationAsync(_client, $"Import Co {i}", 400));
        foreach (var chunk in tasks.Chunk(20))
        {
            await Task.WhenAll(chunk);
        }

        var result = await GhostAsync();

        result.Updated.ShouldBe(count);
        (await GetSummaryAsync()).Count.ShouldBe(0);

        // ...and neither is its undo.
        var undoResponse = await _client.PostAsJsonAsync("/api/applications/stale/ghost/undo",
            new UndoBulkStatusRequest(result.Changes.Select(c => new UndoBulkStatusEntry(c.ApplicationId, c.ToStatus, c.FromStatus)).ToList()),
            JsonOptions);
        undoResponse.EnsureSuccessStatusCode();
        var undo = await undoResponse.Content.ReadFromJsonAsync<UndoBulkStatusResponse>(JsonOptions);
        undo!.Reverted.ShouldBe(count);
        (await GetStatusAsync(result.Changes.First().ApplicationId)).ShouldBe(ApplicationStatus.Applied);
        // Back in Applied, but no longer "stale": the undo is a real status transition inside the
        // horizon, and the definition — like the reminder clock — restarts from the last one. The
        // question does not come back for rows the user has just decided about; the bulk tools on
        // the Applications page do (DECISIONS.md 2026-09-13, panel reminders).
        (await GetSummaryAsync()).Count.ShouldBe(0);
    }

    [Fact]
    public async Task Ghost_Retires_Any_Reminder_On_The_Rows_It_Moves()
    {
        // An open reminder from before the horizon rule existed (the 1,225-row case).
        var stale = await CreateApplicationAsync(_client, "Old Co", 120);
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AfterApply.Infrastructure.Persistence.AppDbContext>();
            var userId = dbContext.Applications.Where(a => a.Id == stale).Select(a => a.UserId).Single();
            dbContext.Reminders.Add(AfterApply.Domain.Notifications.Reminder.Create(userId, stale,
                AfterApply.Domain.Notifications.ReminderType.PossiblyGhosted, DateTimeOffset.UtcNow.AddDays(-120), 120, DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        await GhostAsync();

        var remindersResponse = await _client.GetAsync("/api/reminders");
        remindersResponse.EnsureSuccessStatusCode();
        (await remindersResponse.Content.ReadFromJsonAsync<PagedResult<ReminderResponse>>(JsonOptions))!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Undo_Puts_The_Rows_Back_But_Keeps_Decisions_Made_Since()
    {
        var first = await CreateApplicationAsync(_client, "Old A", 120);
        var second = await CreateApplicationAsync(_client, "Old B", 120);
        var result = await GhostAsync();
        // The user corrected one row by hand between the act and the undo: that decision wins.
        await ChangeStatusAsync(second, ApplicationStatus.Rejected);

        var undoResponse = await _client.PostAsJsonAsync("/api/applications/stale/ghost/undo",
            new UndoBulkStatusRequest(result.Changes.Select(c => new UndoBulkStatusEntry(c.ApplicationId, c.ToStatus, c.FromStatus)).ToList()),
            JsonOptions);
        undoResponse.EnsureSuccessStatusCode();
        var undo = await undoResponse.Content.ReadFromJsonAsync<UndoBulkStatusResponse>(JsonOptions);

        undo!.Reverted.ShouldBe(1);
        undo.Skipped.ShouldBe(1);
        (await GetStatusAsync(first)).ShouldBe(ApplicationStatus.Applied);
        (await GetStatusAsync(second)).ShouldBe(ApplicationStatus.Rejected);
    }

    [Fact]
    public async Task Dismiss_Silences_The_Suggestion_Until_A_Later_Import_Adds_Stale_Rows()
    {
        await CreateApplicationAsync(_client, "Old Co", 120);
        (await GetSummaryAsync()).Suggest.ShouldBeTrue();

        var dismissResponse = await _client.PostAsync("/api/applications/stale/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var silenced = await GetSummaryAsync();
        silenced.Count.ShouldBe(1);
        silenced.Suggest.ShouldBeFalse();

        // A row created after the "not now" — a later import — is a new question.
        await CreateApplicationAsync(_client, "Later Import Co", 200);

        var asked = await GetSummaryAsync();
        asked.Count.ShouldBe(2);
        asked.Suggest.ShouldBeTrue();
    }

    [Fact]
    public async Task Stale_Rows_Are_Scoped_To_The_Caller()
    {
        var stranger = await SignUpAsync("stale.stranger@example.com");
        await CreateApplicationAsync(_client, "Old Co", 120);

        (await GetSummaryAsync(stranger)).Count.ShouldBe(0);

        var strangerGhost = await stranger.PostAsync("/api/applications/stale/ghost", null);
        strangerGhost.EnsureSuccessStatusCode();
        (await strangerGhost.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions))!.Updated.ShouldBe(0);
        (await GetSummaryAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Nightly_Scan_Creates_No_Reminder_For_The_Stale_Batch()
    {
        await CreateApplicationAsync(_client, "Old Co", 120);

        using var scope = _factory!.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<IReminderService>()
            .ScanAndGenerateRemindersAsync(CancellationToken.None);

        created.ShouldBe(0);
    }
}
