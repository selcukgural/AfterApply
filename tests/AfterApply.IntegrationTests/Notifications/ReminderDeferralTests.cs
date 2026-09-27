using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Notifications;

/// <summary>The clock the scan and the list read, so a run can happen "on a Saturday".</summary>
public sealed class ReminderDeferralProfile : IHostProfile
{
    public MutableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public void Configure(IWebHostBuilder builder)
    {
        // Off for every other class (ApiHost); this one is about it.
        builder.UseSetting("Notifications:HoldOutreachOnDaysOff", "true");
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public void Reset() => Clock.Reset();
}

// A follow-up that falls due on a weekend or a Turkish public holiday is held back to the next
// working morning and says why; an outreach-free reminder is not. The applications are made
// through the API and then dated in the database, and the scan and the list are called on the
// service with the moved clock — the HTTP layer keeps the real time its tokens were issued in.
public class ReminderDeferralTests(ApiHost<ReminderDeferralProfile> host)
    : IClassFixture<ApiHost<ReminderDeferralProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;
    private static readonly TimeSpan Istanbul = TimeSpan.FromHours(3);

    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = host.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, host.Services,
            new RegisterRequest("deferral.test@example.com", "P@ssw0rd123!", "De", "Ferral", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        _userId = await host.WithDbAsync(db => db.Users.Where(u => u.Email == "deferral.test@example.com").Select(u => u.Id).SingleAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Follow_Up_Due_On_A_Saturday_Shows_On_Monday_Morning_Saying_Why()
    {
        await CreateAsync("Weekend Co", appliedAt: new DateTimeOffset(2026, 9, 25, 10, 0, 0, Istanbul));

        await ScanAtAsync(new DateTimeOffset(2026, 10, 3, 6, 0, 0, Istanbul)); // Saturday, 8 days on

        var reminder = await host.WithDbAsync(db => db.Reminders.SingleAsync());
        reminder.Type.ShouldBe(ReminderType.FollowUp);
        reminder.DeferredFor.ShouldBe(ReminderDeferral.Weekend);
        reminder.SnoozedUntil.ShouldBe(new DateTimeOffset(2026, 10, 5, 9, 0, 0, Istanbul));
        (await ListAtAsync(new DateTimeOffset(2026, 10, 4, 20, 0, 0, Istanbul))).ShouldBeEmpty();

        var monday = await ListAtAsync(new DateTimeOffset(2026, 10, 5, 9, 5, 0, Istanbul));
        monday.Single().DeferredFor.ShouldBe(ReminderDeferral.Weekend);
        monday.Single().DeferredUntil.ShouldBe(new DateTimeOffset(2026, 10, 5, 9, 0, 0, Istanbul));
    }

    [Fact]
    public async Task A_Turkish_Holiday_Holds_It_For_A_Turkish_Company_Only()
    {
        await CreateAsync("Istanbul Co", appliedAt: new DateTimeOffset(2026, 10, 21, 10, 0, 0, Istanbul));
        var abroad = await CreateAsync("Amsterdam Co", appliedAt: new DateTimeOffset(2026, 10, 21, 10, 0, 0, Istanbul));
        await host.WithDbAsync(db => db.Companies
            .Where(c => db.Applications.Any(a => a.Id == abroad && a.CompanyId == c.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Country, "NL")));

        await ScanAtAsync(new DateTimeOffset(2026, 10, 29, 6, 0, 0, Istanbul)); // Republic Day, Thursday

        var reminders = await host.WithDbAsync(db => db.Reminders.ToListAsync());
        reminders.Single(r => r.ApplicationId != abroad).DeferredFor.ShouldBe(ReminderDeferral.RepublicDay);
        reminders.Single(r => r.ApplicationId != abroad).SnoozedUntil.ShouldBe(new DateTimeOffset(2026, 10, 30, 9, 0, 0, Istanbul));
        reminders.Single(r => r.ApplicationId == abroad).DeferredFor.ShouldBeNull();
        reminders.Single(r => r.ApplicationId == abroad).SnoozedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task A_Reminder_That_Asks_Nothing_Of_The_Company_Is_Not_Held()
    {
        // 31 days of silence: "possibly ghosted" — a question for the user, not an e-mail to send.
        await CreateAsync("Silent Co", appliedAt: new DateTimeOffset(2026, 9, 2, 10, 0, 0, Istanbul));

        await ScanAtAsync(new DateTimeOffset(2026, 10, 3, 6, 0, 0, Istanbul));

        var reminder = await host.WithDbAsync(db => db.Reminders.SingleAsync());
        reminder.Type.ShouldBe(ReminderType.PossiblyGhosted);
        reminder.DeferredFor.ShouldBeNull();
        reminder.SnoozedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task The_Users_Own_Snooze_Replaces_The_Reason()
    {
        await CreateAsync("Weekend Co", appliedAt: new DateTimeOffset(2026, 9, 25, 10, 0, 0, Istanbul));
        await ScanAtAsync(new DateTimeOffset(2026, 10, 3, 6, 0, 0, Istanbul));
        var id = await host.WithDbAsync(db => db.Reminders.Select(r => r.Id).SingleAsync());

        host.Profile.Clock.Set(new DateTimeOffset(2026, 10, 5, 9, 30, 0, Istanbul));
        await host.WithScopeAsync(services =>
            services.GetRequiredService<IReminderService>().SnoozeAsync(_userId, id, new SnoozeReminderRequest(3), CancellationToken.None));

        var reminder = await host.WithDbAsync(db => db.Reminders.SingleAsync());
        reminder.DeferredFor.ShouldBeNull();
        reminder.SnoozedUntil.ShouldBe(new DateTimeOffset(2026, 10, 8, 9, 30, 0, Istanbul));
    }

    private async Task<Guid> CreateAsync(string company, DateTimeOffset appliedAt)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.Id;
        await host.WithDbAsync(db => db.Applications.Where(a => a.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.AppliedAt, appliedAt)));
        return id;
    }

    private async Task ScanAtAsync(DateTimeOffset at)
    {
        host.Profile.Clock.Set(at);
        await host.WithScopeAsync(services =>
            services.GetRequiredService<IReminderService>().ScanAndGenerateRemindersAsync(CancellationToken.None));
    }

    private async Task<IReadOnlyCollection<ReminderResponse>> ListAtAsync(DateTimeOffset at)
    {
        host.Profile.Clock.Set(at);
        await host.ClearCachesAsync();
        IReadOnlyCollection<ReminderResponse> items = [];
        await host.WithScopeAsync(async services =>
            items = (await services.GetRequiredService<IReminderService>()
                .GetActiveRemindersAsync(_userId, new GetRemindersQuery(1, 50), CancellationToken.None)).Items);
        return items;
    }
}
