using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

[Collection(IntegrationTestCollection.Name)]
public class BulkOperationTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private HttpClient _otherUserClient = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(BulkOperationTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _client = await SignUpAsync("bulk.owner@example.com");
        _otherUserClient = await SignUpAsync("bulk.stranger@example.com");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task<HttpClient> SignUpAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Bulk", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string companyName, string jobTitle = "Engineer")
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, jobTitle, null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<ApplicationStatus> GetStatusAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}");
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return detail!.Status;
    }

    private async Task<IReadOnlyCollection<ApplicationStatusHistoryResponse>> GetHistoryAsync(Guid applicationId)
    {
        var response = await _client.GetAsync($"/api/applications/{applicationId}/status-history");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ApplicationStatusHistoryResponse>>(JsonOptions))!;
    }

    [Fact]
    public async Task Bulk_Status_Change_Moves_Every_Selected_Application()
    {
        var first = await CreateApplicationAsync(_client, "Bulk Change Co A");
        var second = await CreateApplicationAsync(_client, "Bulk Change Co B");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [first, second]), ApplicationStatus.Rejected, "Batch note"), JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions);

        result!.Updated.ShouldBe(2);
        result.SkippedAlreadyInStatus.ShouldBe(0);
        (await GetStatusAsync(first)).ShouldBe(ApplicationStatus.Rejected);
        (await GetStatusAsync(second)).ShouldBe(ApplicationStatus.Rejected);
    }

    [Fact]
    public async Task Bulk_Status_Change_Records_Its_Own_Origin_And_The_Shared_Note()
    {
        var id = await CreateApplicationAsync(_client, "Bulk Origin Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [id]), ApplicationStatus.Interview, "August cleanup"), JsonOptions);
        response.EnsureSuccessStatusCode();

        var history = await GetHistoryAsync(id);
        var latest = history.First();

        // BulkEdit, not Manual: a change applied to a whole selection is a different act from one
        // made while looking at a single application, and the history has to say which it was.
        latest.Origin.ShouldBe(StatusChangeOrigin.BulkEdit);
        latest.ToStatus.ShouldBe(ApplicationStatus.Interview);
        latest.Note.ShouldBe("August cleanup");
    }

    [Fact]
    public async Task Bulk_Status_Change_Skips_Applications_Already_In_The_Target_Status()
    {
        var alreadyRejected = await CreateApplicationAsync(_client, "Already Rejected Co");
        await ChangeStatusAsync(alreadyRejected, ApplicationStatus.Rejected);
        var stillApplied = await CreateApplicationAsync(_client, "Still Applied Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [alreadyRejected, stillApplied]), ApplicationStatus.Rejected), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions);

        result!.Updated.ShouldBe(1);
        result.SkippedAlreadyInStatus.ShouldBe(1);
        // Skipped means untouched, not "written again": the already-rejected row keeps exactly the
        // history it had, so a no-op does not pad the timeline.
        result.Changes.Select(c => c.ApplicationId).ShouldBe([stillApplied]);
        (await GetHistoryAsync(alreadyRejected)).Count(h => h.Origin == StatusChangeOrigin.BulkEdit).ShouldBe(0);
    }

    [Fact]
    public async Task Bulk_Status_Change_Never_Touches_Another_Users_Application()
    {
        var mine = await CreateApplicationAsync(_client, "Mine Co");
        var theirs = await CreateApplicationAsync(_otherUserClient, "Theirs Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [mine, theirs]), ApplicationStatus.Rejected), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions);

        // The foreign id matches nothing rather than erroring — an id in a request body is a claim,
        // and the response must not confirm whether someone else's application exists.
        result!.Updated.ShouldBe(1);
        result.Changes.Single().ApplicationId.ShouldBe(mine);

        var theirDetail = await _otherUserClient.GetAsync($"/api/applications/{theirs}");
        theirDetail.EnsureSuccessStatusCode();
        var untouched = await theirDetail.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        untouched!.Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Undo_Puts_Every_Changed_Application_Back()
    {
        var interviewing = await CreateApplicationAsync(_client, "Undo Interview Co");
        await ChangeStatusAsync(interviewing, ApplicationStatus.Interview);
        var applied = await CreateApplicationAsync(_client, "Undo Applied Co");

        var changeResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [interviewing, applied]), ApplicationStatus.Rejected), JsonOptions);
        changeResponse.EnsureSuccessStatusCode();
        var change = await changeResponse.Content.ReadFromJsonAsync<BulkChangeStatusResponse>(JsonOptions);

        var undoResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status/undo", new UndoBulkStatusRequest(
            [.. change!.Changes.Select(c => new UndoBulkStatusEntry(c.ApplicationId, c.ToStatus, c.FromStatus))]),
            JsonOptions);
        undoResponse.EnsureSuccessStatusCode();
        var undo = await undoResponse.Content.ReadFromJsonAsync<UndoBulkStatusResponse>(JsonOptions);

        undo!.Reverted.ShouldBe(2);
        undo.Skipped.ShouldBe(0);
        // Each goes back to where it individually was, not to a single shared "before" status.
        (await GetStatusAsync(interviewing)).ShouldBe(ApplicationStatus.Interview);
        (await GetStatusAsync(applied)).ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Undo_Appends_History_Rather_Than_Erasing_The_Change_It_Reverses()
    {
        var id = await CreateApplicationAsync(_client, "Undo History Co");

        var changeResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [id]), ApplicationStatus.Rejected), JsonOptions);
        changeResponse.EnsureSuccessStatusCode();

        var undoResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status/undo", new UndoBulkStatusRequest(
            [new UndoBulkStatusEntry(id, ApplicationStatus.Rejected, ApplicationStatus.Applied)]), JsonOptions);
        undoResponse.EnsureSuccessStatusCode();

        var history = await GetHistoryAsync(id);

        // The mistake stays on the record. Both rows are there, newest first, and the undo carries
        // its own origin so it reads as a correction rather than as an ordinary edit.
        history.Count(h => h.Origin == StatusChangeOrigin.BulkEdit).ShouldBe(1);
        history.Count(h => h.Origin == StatusChangeOrigin.BulkEditReverted).ShouldBe(1);
        history.First().Origin.ShouldBe(StatusChangeOrigin.BulkEditReverted);
        history.First().ToStatus.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Undo_Leaves_Alone_An_Application_Whose_Status_Moved_On()
    {
        var moved = await CreateApplicationAsync(_client, "Moved On Co");
        var untouched = await CreateApplicationAsync(_client, "Untouched Co");

        var changeResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(Ids: [moved, untouched]), ApplicationStatus.Rejected), JsonOptions);
        changeResponse.EnsureSuccessStatusCode();

        // The user corrects one row by hand after the bulk change but before pressing undo.
        await ChangeStatusAsync(moved, ApplicationStatus.Offer);

        var undoResponse = await _client.PostAsJsonAsync("/api/applications/bulk/status/undo", new UndoBulkStatusRequest(
        [
            new UndoBulkStatusEntry(moved, ApplicationStatus.Rejected, ApplicationStatus.Applied),
            new UndoBulkStatusEntry(untouched, ApplicationStatus.Rejected, ApplicationStatus.Applied)
        ]), JsonOptions);
        undoResponse.EnsureSuccessStatusCode();
        var undo = await undoResponse.Content.ReadFromJsonAsync<UndoBulkStatusResponse>(JsonOptions);

        undo!.Reverted.ShouldBe(1);
        undo.Skipped.ShouldBe(1);
        // A decision made after the change being undone always wins.
        (await GetStatusAsync(moved)).ShouldBe(ApplicationStatus.Offer);
        (await GetStatusAsync(untouched)).ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Bulk_Delete_Removes_The_Selected_Applications_Only()
    {
        var doomed = await CreateApplicationAsync(_client, "Doomed Co");
        var survivor = await CreateApplicationAsync(_client, "Survivor Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete",
            new BulkDeleteRequest(new BulkSelection(Ids: [doomed])), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkDeleteResponse>(JsonOptions);

        result!.Deleted.ShouldBe(1);
        (await _client.GetAsync($"/api/applications/{doomed}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/applications/{survivor}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Bulk_Delete_Never_Touches_Another_Users_Application()
    {
        var mine = await CreateApplicationAsync(_client, "My Deletable Co");
        var theirs = await CreateApplicationAsync(_otherUserClient, "Their Safe Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete",
            new BulkDeleteRequest(new BulkSelection(Ids: [mine, theirs])), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkDeleteResponse>(JsonOptions);

        result!.Deleted.ShouldBe(1);
        (await _otherUserClient.GetAsync($"/api/applications/{theirs}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Bulk_Delete_By_Filter_Deletes_Exactly_What_The_List_Would_Have_Shown()
    {
        var rejectedA = await CreateApplicationAsync(_client, "Filter Rejected A");
        var rejectedB = await CreateApplicationAsync(_client, "Filter Rejected B");
        await ChangeStatusAsync(rejectedA, ApplicationStatus.Rejected);
        await ChangeStatusAsync(rejectedB, ApplicationStatus.Rejected);
        var applied = await CreateApplicationAsync(_client, "Filter Applied C");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete", new BulkDeleteRequest(
            new BulkSelection(AllMatching: new BulkFilterSelection(Status: ApplicationStatus.Rejected)),
            ExpectedCount: 2), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkDeleteResponse>(JsonOptions);

        result!.Deleted.ShouldBe(2);
        (await _client.GetAsync($"/api/applications/{applied}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Bulk_Delete_By_Filter_Refuses_And_Changes_Nothing_When_The_Count_Has_Moved()
    {
        await CreateApplicationAsync(_client, "Stale Screen Co A");
        await CreateApplicationAsync(_client, "Stale Screen Co B");

        // The screen was drawn when there was one application; a second arrived before the button
        // was pressed. Deleting "all of them" now would sweep in a row nobody agreed to.
        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete", new BulkDeleteRequest(
            new BulkSelection(AllMatching: new BulkFilterSelection()), ExpectedCount: 1), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("errorCode").GetString().ShouldBe("BULK_COUNT_MISMATCH");
        problem.GetProperty("expectedCount").GetInt32().ShouldBe(1);
        problem.GetProperty("actualCount").GetInt32().ShouldBe(2);

        var list = await _client.GetFromJsonAsync<PagedResult<ApplicationSummaryResponse>>("/api/applications", JsonOptions);
        list!.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Bulk_Status_Change_By_Filter_Refuses_When_The_Count_Has_Moved()
    {
        await CreateApplicationAsync(_client, "Status Guard Co A");
        await CreateApplicationAsync(_client, "Status Guard Co B");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/status", new BulkChangeStatusRequest(
            new BulkSelection(AllMatching: new BulkFilterSelection()), ApplicationStatus.Ghosted,
            ExpectedCount: 5), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var list = await _client.GetFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(
            "/api/applications?status=Ghosted", JsonOptions);
        list!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Bulk_Delete_By_Search_Filter_Matches_Company_Name_The_Way_The_List_Does()
    {
        await CreateApplicationAsync(_client, "Searchable Bulk Robotics", "Firmware Engineer");
        await CreateApplicationAsync(_client, "Unrelated Bulk Co", "Backend Engineer");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete", new BulkDeleteRequest(
            new BulkSelection(AllMatching: new BulkFilterSelection(Search: "Searchable Bulk")), ExpectedCount: 1),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BulkDeleteResponse>(JsonOptions);

        result!.Deleted.ShouldBe(1);
    }

    [Fact]
    public async Task Bulk_Request_Naming_Both_Selection_Forms_Is_Rejected()
    {
        var id = await CreateApplicationAsync(_client, "Ambiguous Selection Co");

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete", new BulkDeleteRequest(
            new BulkSelection(Ids: [id], AllMatching: new BulkFilterSelection()), ExpectedCount: 1), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAsync($"/api/applications/{id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Bulk_Request_With_An_Empty_Id_List_Is_Rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete",
            new BulkDeleteRequest(new BulkSelection(Ids: [])), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
