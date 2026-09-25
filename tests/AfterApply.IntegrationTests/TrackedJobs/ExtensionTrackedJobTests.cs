using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.TrackedJobs;

// The extension's "Apply later" (POST /api/tracked-jobs/from-extension, 0.9.2) and what "I Applied"
// does to a posting saved that way: the saved row becomes the application instead of sitting next
// to it, carrying what only it knew.
[Collection(IntegrationTestCollection.Name)]
public class ExtensionTrackedJobTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string JobUrl = "https://www.linkedin.com/jobs/view/4468983973/";

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, _factory.Services,
            new RegisterRequest("later.test@example.com", "P@ssw0rd123!", "Later", "Test", true));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateFromExtensionRequest Capture(string? hrName = "Seila Ponce Rico", string? descriptionHtml = "<p>Lead the team.</p>") =>
        new("StaffingBird", "Engineering Lead", JobUrl, "The Randstad, Netherlands", "Lead the team.",
            DateTimeOffset.UtcNow.AddDays(-2), descriptionHtml,
            CompanyLinkedInUrl: null, CompanyKariyerNetUrl: null,
            HrName: hrName, HrLinkedInUrl: hrName is null ? null : "https://www.linkedin.com/in/seila-ponce-rico-707905144/");

    private async Task<ExtensionTrackedJobResponse> SaveForLaterAsync(HttpClient client, CreateFromExtensionRequest request,
        HttpStatusCode expected)
    {
        var response = await client.PostAsJsonAsync("/api/tracked-jobs/from-extension", request, JsonOptions);
        response.StatusCode.ShouldBe(expected);
        return (await response.Content.ReadFromJsonAsync<ExtensionTrackedJobResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Saves_The_Capture_With_Its_Job_And_Description()
    {
        var result = await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);

        result.Outcome.ShouldBe(ExtensionTrackedJobOutcome.Saved);
        var saved = await host.WithDbAsync(db => db.TrackedJobs.SingleAsync());
        saved.JobTitle.ShouldBe("Engineering Lead");
        saved.JobUrl.ShouldBe(JobUrl);
        saved.HrName.ShouldBe("Seila Ponce Rico");
        saved.CapturedJobDescriptionHtml.ShouldBe("<p>Lead the team.</p>");
        saved.JobId.ShouldNotBeNull();
        (await host.WithDbAsync(db => db.Jobs.SingleAsync(j => j.Id == saved.JobId))).Source.ShouldBe(Source.LinkedIn);
    }

    [Fact]
    public async Task Saving_The_Same_Url_Twice_Writes_Nothing_The_Second_Time()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);

        var again = await SaveForLaterAsync(_client, Capture(), HttpStatusCode.OK);

        again.Outcome.ShouldBe(ExtensionTrackedJobOutcome.AlreadySaved);
        (await host.WithDbAsync(db => db.TrackedJobs.CountAsync())).ShouldBe(1);
    }

    [Fact]
    public async Task A_Url_Already_Applied_To_Is_Not_Saved_For_Later()
    {
        (await _client.PostAsJsonAsync("/api/applications/from-extension", Capture(), JsonOptions)).EnsureSuccessStatusCode();

        var result = await SaveForLaterAsync(_client, Capture(), HttpStatusCode.OK);

        result.Outcome.ShouldBe(ExtensionTrackedJobOutcome.AlreadyApplied);
        (await host.WithDbAsync(db => db.TrackedJobs.CountAsync())).ShouldBe(0);
    }

    // The response is an outcome and nothing else: the extension token must not become a way to
    // read stored rows back (see ExtensionTrackedJobResponse).
    [Fact]
    public async Task The_Response_Carries_No_Record()
    {
        var response = await _client.PostAsJsonAsync("/api/tracked-jobs/from-extension", Capture(), JsonOptions);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["outcome"]);
    }

    [Fact]
    public async Task Another_Users_Saved_Url_Does_Not_Count_As_Saved()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);

        var other = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(other, _factory.Services,
            new RegisterRequest("later.other@example.com", "P@ssw0rd123!", "Other", "User", true));
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        (await SaveForLaterAsync(other, Capture(), HttpStatusCode.Created)).Outcome.ShouldBe(ExtensionTrackedJobOutcome.Saved);
    }

    [Fact]
    public async Task The_Extension_Token_Reaches_It()
    {
        var tokenResponse = await _client.PostAsJsonAsync("/api/personal-access-tokens",
            new CreatePersonalAccessTokenRequest("Chrome Extension", PersonalAccessTokenScope.Extension), JsonOptions);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<CreatedPersonalAccessTokenResponse>(JsonOptions))!;
        using var extension = _factory.CreateClient();
        extension.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        (await SaveForLaterAsync(extension, Capture(), HttpStatusCode.Created)).Outcome.ShouldBe(ExtensionTrackedJobOutcome.Saved);
        // ...and still nothing else under the same route group.
        (await extension.GetAsync("/api/tracked-jobs")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_A_Profile_Url_Outside_The_Allow_List_Like_I_Applied_Does()
    {
        var request = Capture() with { HrLinkedInUrl = "https://evil.example.com/in/someone" };

        var response = await _client.PostAsJsonAsync("/api/tracked-jobs/from-extension", request, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task I_Applied_On_A_Saved_Url_Turns_It_Into_The_Application()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);
        await host.WithDbAsync(db => db.TrackedJobs.ExecuteUpdateAsync(s => s.SetProperty(t => t.Notes, "Ask about remote days")));

        // The page no longer shows the hiring team or the description (the posting was edited):
        // what the saved row knew is carried over rather than lost.
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            Capture(hrName: null, descriptionHtml: null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!;
        result.WasDuplicate.ShouldBeFalse();
        result.FromTrackedJob.ShouldBeTrue();
        result.Application.HrName.ShouldBe("Seila Ponce Rico");
        result.Application.Notes.ShouldBe("Ask about remote days");
        result.Application.Source.ShouldBe(Source.BrowserExtension);

        (await host.WithDbAsync(db => db.TrackedJobs.CountAsync())).ShouldBe(0);
        var application = await host.WithDbAsync(db => db.Applications.SingleAsync());
        application.CapturedJobDescriptionHtml.ShouldBe("<p>Lead the team.</p>");
    }

    [Fact]
    public async Task I_Applied_Prefers_What_The_Form_Holds_Now()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);

        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            Capture(hrName: "Someone Newer", descriptionHtml: "<p>Updated posting.</p>"), JsonOptions);

        var result = (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!;
        result.Application.HrName.ShouldBe("Someone Newer");
        (await host.WithDbAsync(db => db.Applications.SingleAsync())).CapturedJobDescriptionHtml.ShouldBe("<p>Updated posting.</p>");
    }

    [Fact]
    public async Task I_Applied_Without_A_Saved_Url_Says_It_Was_Not_Converted()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension", Capture(), JsonOptions);

        (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!.FromTrackedJob.ShouldBeFalse();
    }

    // The site's own "convert" button (the tracked-jobs page) carries the capture over too.
    [Fact]
    public async Task Converting_On_The_Site_Keeps_The_Job_And_Description()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);
        var saved = await host.WithDbAsync(db => db.TrackedJobs.SingleAsync());

        var response = await _client.PostAsJsonAsync($"/api/tracked-jobs/{saved.Id}/convert",
            new ConvertTrackedJobRequest(EmploymentType.FullTime, DateTimeOffset.UtcNow, null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
        detail.JobDescriptionHtml.ShouldBe("<p>Lead the team.</p>");
        var application = await host.WithDbAsync(db => db.Applications.SingleAsync());
        application.JobId.ShouldBe(saved.JobId);
    }

    [Fact]
    public async Task Saved_Jobs_Are_In_The_Data_Export()
    {
        await SaveForLaterAsync(_client, Capture(), HttpStatusCode.Created);

        var export = await _client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions);

        var item = export!.TrackedJobs.ShouldNotBeNull().ShouldHaveSingleItem();
        item.CompanyName.ShouldBe("StaffingBird");
        item.JobUrl.ShouldBe(JobUrl);
        item.HrName.ShouldBe("Seila Ponce Rico");
    }
}
