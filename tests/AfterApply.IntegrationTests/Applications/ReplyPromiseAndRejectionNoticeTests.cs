using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

/// <summary>
/// The two answers the status panel and the application page collect — the company's promised
/// reply date and how a rejection was learned of — through the real endpoints and database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ReplyPromiseAndRejectionNoticeTests(ApiHost<DefaultProfile> host)
    : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient _client = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        (_client, var auth) = await host.RegisterAsync("promise.owner@example.com");
        _userId = auth.User.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ApplicationDetailResponse> CreateAsync(string company, int appliedDaysAgo = 10)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Backend Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-appliedDaysAgo), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(Guid id, ChangeStatusRequest request) =>
        _client.PostAsJsonAsync($"/api/applications/{id}/status", request, JsonOptions);

    private Task<HttpResponseMessage> SetPromiseAsync(Guid id, DateOnly? promisedBy, HttpClient? client = null) =>
        (client ?? _client).PutAsJsonAsync($"/api/applications/{id}/reply-promise", new SetReplyPromiseRequest(promisedBy), JsonOptions);

    private static async Task<ApplicationDetailResponse> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task A_Date_Set_From_The_Page_Belongs_To_The_Current_Stage_And_Reads_As_Pending()
    {
        var created = await CreateAsync("Promise Page Co");

        var detail = await ReadAsync(await SetPromiseAsync(created.Id, Today.AddDays(5)));

        detail.PromisedReplyBy.ShouldBe(Today.AddDays(5));
        detail.PromisedReplyStatus.ShouldBe(ApplicationStatus.Applied);
        detail.PromisedReplyOutcome.ShouldBe(ReplyPromiseOutcome.Pending);

        var reread = await ReadAsync(await _client.GetAsync($"/api/applications/{created.Id}"));
        reread.PromisedReplyBy.ShouldBe(Today.AddDays(5));
    }

    [Fact]
    public async Task A_Date_Already_Past_Reads_As_Overdue_And_Null_Clears_It()
    {
        var created = await CreateAsync("Promise Past Co");

        (await ReadAsync(await SetPromiseAsync(created.Id, Today.AddDays(-3)))).PromisedReplyOutcome
            .ShouldBe(ReplyPromiseOutcome.Overdue);

        var cleared = await ReadAsync(await SetPromiseAsync(created.Id, null));
        cleared.PromisedReplyBy.ShouldBeNull();
        cleared.PromisedReplyStatus.ShouldBeNull();
        cleared.PromisedReplyOutcome.ShouldBeNull();
    }

    [Fact]
    public async Task A_Date_Given_With_A_Status_Change_Belongs_To_The_Stage_That_Change_Opens()
    {
        var created = await CreateAsync("Promise Panel Co");

        var detail = await ReadAsync(await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Interview, null, null, Today.AddDays(4))));

        detail.Status.ShouldBe(ApplicationStatus.Interview);
        detail.PromisedReplyStatus.ShouldBe(ApplicationStatus.Interview);
        detail.PromisedReplyOutcome.ShouldBe(ReplyPromiseOutcome.Pending);
    }

    [Fact]
    public async Task The_Next_Status_Change_Answers_The_Promise()
    {
        var created = await CreateAsync("Promise Kept Co");
        await ReadAsync(await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Interview, null, DateTimeOffset.UtcNow.AddDays(-5), Today.AddDays(2))));

        var answered = await ReadAsync(await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Offer, null, null)));

        // Kept, and the promise stays on the application: its outcome is the record.
        answered.PromisedReplyBy.ShouldBe(Today.AddDays(2));
        answered.PromisedReplyOutcome.ShouldBe(ReplyPromiseOutcome.Kept);
    }

    [Fact]
    public async Task A_Closing_Status_With_A_Date_Is_Refused_And_Nothing_Changes()
    {
        var created = await CreateAsync("Promise Closing Co");

        var response = await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, Today.AddDays(3)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(await _client.GetAsync($"/api/applications/{created.Id}"))).Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task A_Closed_Application_Cannot_Be_Given_A_Date_From_The_Page()
    {
        var created = await CreateAsync("Promise Closed Co");
        await ReadAsync(await ChangeStatusAsync(created.Id, new ChangeStatusRequest(ApplicationStatus.Withdrawn, null, null)));

        var response = await SetPromiseAsync(created.Id, Today.AddDays(3));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Date_More_Than_A_Year_Away_Is_Refused()
    {
        var created = await CreateAsync("Promise Far Co");

        (await SetPromiseAsync(created.Id, Today.AddDays(400))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_Users_Application_Is_Not_Found_And_Untouched()
    {
        var created = await CreateAsync("Promise Owner Co");
        var (stranger, _) = await host.RegisterAsync("promise.stranger@example.com");

        (await SetPromiseAsync(created.Id, Today.AddDays(3), stranger)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync($"/api/applications/{created.Id}/status",
                new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, null, RejectionNotice.SeenOnPortal), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var mine = await ReadAsync(await _client.GetAsync($"/api/applications/{created.Id}"));
        mine.PromisedReplyBy.ShouldBeNull();
        mine.Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task A_Manual_Rejection_Carries_What_The_User_Said_And_An_Undo_Takes_It_Away()
    {
        var created = await CreateAsync("Notice Manual Co");

        var rejected = await ReadAsync(await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, null, RejectionNotice.SeenOnPortal)));
        rejected.RejectionNotice.ShouldBe(RejectionNotice.SeenOnPortal);

        var reopened = await ReadAsync(await ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(ApplicationStatus.Interview, null, null)));
        reopened.RejectionNotice.ShouldBeNull();
    }

    [Fact]
    public async Task A_Rejection_Notice_With_Another_Status_Is_Refused()
    {
        var created = await CreateAsync("Notice Wrong Co");

        (await ChangeStatusAsync(created.Id,
                new ChangeStatusRequest(ApplicationStatus.Offer, null, null, null, RejectionNotice.CompanyNotified)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Rejection_Applied_From_The_Companys_Email_Is_CompanyNotified()
    {
        var created = await CreateAsync("Notice Email Co");

        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IApplicationService>();
            await service.ChangeStatusAsync(_userId, created.Id, ApplicationStatus.Rejected, DateTimeOffset.UtcNow,
                new StatusChangeContext(Source.Email, StatusChangeOrigin.EmailAutoApplied), CancellationToken.None);
        }

        (await ReadAsync(await _client.GetAsync($"/api/applications/{created.Id}"))).RejectionNotice
            .ShouldBe(RejectionNotice.CompanyNotified);
    }

    [Fact]
    public async Task The_Account_Export_Carries_Both_Answers()
    {
        var promised = await CreateAsync("Export Promise Co");
        await ReadAsync(await SetPromiseAsync(promised.Id, Today.AddDays(6)));
        var rejected = await CreateAsync("Export Notice Co");
        await ReadAsync(await ChangeStatusAsync(rejected.Id,
            new ChangeStatusRequest(ApplicationStatus.Rejected, null, null, null, RejectionNotice.OtherOrInferred)));

        var export = (await (await _client.GetAsync("/api/users/me/export")).Content
            .ReadFromJsonAsync<AccountExportResponse>(JsonOptions))!;

        var promisedItem = export.Applications.Single(a => a.Id == promised.Id);
        promisedItem.PromisedReplyBy.ShouldBe(Today.AddDays(6));
        promisedItem.PromisedReplyStatus.ShouldBe(ApplicationStatus.Applied);
        export.Applications.Single(a => a.Id == rejected.Id).RejectionNotice.ShouldBe(RejectionNotice.OtherOrInferred);
    }
}
