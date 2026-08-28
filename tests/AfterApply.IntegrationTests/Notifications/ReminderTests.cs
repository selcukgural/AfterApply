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
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Notifications;

public class ReminderTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = await CreateAuthenticatedClientAsync("reminders.test@example.com");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
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
}
