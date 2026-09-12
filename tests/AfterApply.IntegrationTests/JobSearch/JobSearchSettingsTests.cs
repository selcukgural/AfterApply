using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>Per-user settings: the user's preferences on one route, the admin's limits on
/// another, each blind to the other's columns; and the row disappearing once nothing is set.</summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchSettingsTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string UserEmail = "settings.user@example.com";
    private const string AdminEmail = "settings.admin@ekariyerim.com";

    private JobSearchTestHost _host = null!;
    private HttpClient _user = null!;
    private HttpClient _admin = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _host = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchSettingsTests));
        _user = await _host.RegisterAsync(UserEmail);
        _admin = await _host.RegisterAsync(AdminEmail);
        await _host.SetAdminAsync(AdminEmail);
        _userId = await _host.UserIdAsync(UserEmail);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task With_No_Row_The_Effective_Values_Are_The_Global_Ones()
    {
        var settings = await _user.GetFromJsonAsync<JobSearchSettingsResponse>("/api/job-search/settings", JobSearchTestHost.Json);

        settings!.UserOverrides.ShouldBeNull();
        settings.Effective.Country.ShouldBe("tr");
        settings.Effective.Language.ShouldBeNull();
        settings.Effective.PerUserDailyCredits.ShouldBe(10);
        settings.GlobalDefaults.MaxPagesPerSearch.ShouldBe(3);
    }

    [Fact]
    public async Task Preferences_Create_The_Row_And_Change_The_Effective_Values()
    {
        var response = await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("DE", "en", " Berlin ", JobSearchDatePosted.Week, true), JobSearchTestHost.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<JobSearchSettingsResponse>(JobSearchTestHost.Json);
        settings!.UserOverrides.ShouldNotBeNull();
        settings.UserOverrides.DefaultCountry.ShouldBe("de");
        settings.UserOverrides.DefaultLanguage.ShouldBe("en");
        settings.UserOverrides.DefaultLocation.ShouldBe("Berlin");
        settings.UserOverrides.DefaultDatePosted.ShouldBe(JobSearchDatePosted.Week);
        settings.UserOverrides.DefaultWorkFromHome.ShouldBe(true);
        settings.UserOverrides.PerUserDailyCredits.ShouldBeNull();
        settings.Effective.Country.ShouldBe("de");
        settings.Effective.DatePosted.ShouldBe(JobSearchDatePosted.Week);
        settings.Effective.WorkFromHome.ShouldBeTrue();
        settings.Effective.PerUserDailyCredits.ShouldBe(10);
    }

    [Fact]
    public async Task Saved_Preferences_Shape_The_Next_Search()
    {
        (await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest(null, null, "İstanbul", JobSearchDatePosted.Month, true), JobSearchTestHost.Json)).EnsureSuccessStatusCode();

        (await _user.GetAsync("/api/job-search/jobs?query=backend")).EnsureSuccessStatusCode();
        (await _user.GetAsync("/api/job-search/jobs?query=backend&workFromHome=false&datePosted=All")).EnsureSuccessStatusCode();

        var uris = _host.Handler.UrisFor("/search-v2");
        uris[0].Query.ShouldBe("?query=backend&country=tr&location=%C4%B0stanbul&date_posted=month&work_from_home=true");
        uris[1].Query.ShouldBe("?query=backend&country=tr&location=%C4%B0stanbul");
    }

    [Fact]
    public async Task The_User_Route_Cannot_Touch_The_Limits()
    {
        var aliceLimits = await _admin.PutAsJsonAsync($"/api/admin/job-search/settings/{_userId}",
            new UpdateJobSearchLimitsRequest(20, 2, 3), JobSearchTestHost.Json);
        aliceLimits.EnsureSuccessStatusCode();

        // A body that names the limit columns on the user route: the request type has no such
        // members, so they are simply not read.
        var response = await _user.PutAsJsonAsync("/api/job-search/settings", new
        {
            defaultCountry = "nl",
            perUserDailyCredits = 999,
            maxPagesPerSearch = 20
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<JobSearchSettingsResponse>(JobSearchTestHost.Json);
        settings!.UserOverrides!.DefaultCountry.ShouldBe("nl");
        settings.UserOverrides.PerUserDailyCredits.ShouldBe(20);
        settings.UserOverrides.MaxPagesPerSearch.ShouldBe(2);
        settings.Effective.PerUserDailyCredits.ShouldBe(20);
    }

    [Fact]
    public async Task The_Admin_Route_Cannot_Touch_The_Preferences_And_Needs_An_Admin()
    {
        (await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("nl", null, null, null, null), JobSearchTestHost.Json)).EnsureSuccessStatusCode();

        var forbidden = await _user.PutAsJsonAsync($"/api/admin/job-search/settings/{_userId}",
            new UpdateJobSearchLimitsRequest(50, null, null), JobSearchTestHost.Json);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var allowed = await _admin.PutAsJsonAsync($"/api/admin/job-search/settings/{_userId}",
            new UpdateJobSearchLimitsRequest(50, null, 8), JobSearchTestHost.Json);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await allowed.Content.ReadFromJsonAsync<JobSearchSettingsResponse>(JobSearchTestHost.Json);
        settings!.UserOverrides!.DefaultCountry.ShouldBe("nl");
        settings.UserOverrides.PerUserDailyCredits.ShouldBe(50);
        settings.Effective.MaxJobIdsPerDetails.ShouldBe(8);

        var read = await _admin.GetFromJsonAsync<JobSearchSettingsResponse>($"/api/admin/job-search/settings/{_userId}", JobSearchTestHost.Json);
        read!.Effective.PerUserDailyCredits.ShouldBe(50);

        (await _admin.GetAsync($"/api/admin/job-search/settings/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Raised_Id_Cap_Lets_A_Bigger_Batch_Through()
    {
        (await _user.GetAsync("/api/job-search/jobs/details?ids=a,b,c,d,e,f")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await _admin.PutAsJsonAsync($"/api/admin/job-search/settings/{_userId}",
            new UpdateJobSearchLimitsRequest(null, null, 8), JobSearchTestHost.Json)).EnsureSuccessStatusCode();

        (await _user.GetAsync("/api/job-search/jobs/details?ids=a,b,c,d,e,f")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Clearing_Every_Override_Removes_The_Row()
    {
        (await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("nl", null, null, null, null), JobSearchTestHost.Json)).EnsureSuccessStatusCode();
        (await _host.QueryAsync(db => db.JobSearchUserSettings.CountAsync(s => s.UserId == _userId))).ShouldBe(1);

        var cleared = await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest(null, null, null, null, null), JobSearchTestHost.Json);

        cleared.EnsureSuccessStatusCode();
        (await cleared.Content.ReadFromJsonAsync<JobSearchSettingsResponse>(JobSearchTestHost.Json))!.UserOverrides.ShouldBeNull();
        (await _host.QueryAsync(db => db.JobSearchUserSettings.CountAsync(s => s.UserId == _userId))).ShouldBe(0);
    }

    [Fact]
    public async Task Invalid_Preferences_Are_Refused()
    {
        var response = await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("TUR", null, null, null, null), JobSearchTestHost.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_The_Account_Takes_The_Settings_And_The_Ledger_With_It()
    {
        (await _user.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("nl", null, null, null, null), JobSearchTestHost.Json)).EnsureSuccessStatusCode();
        (await _user.GetAsync("/api/job-search/jobs?query=cascade")).EnsureSuccessStatusCode();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new { password = "P@ssw0rd123!" })
        };
        (await _user.SendAsync(delete)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _host.QueryAsync(db => db.JobSearchUserSettings.CountAsync(s => s.UserId == _userId))).ShouldBe(0);
        (await _host.QueryAsync(db => db.JobSearchUsages.CountAsync(u => u.UserId == _userId))).ShouldBe(0);
        // The shared tables stay — they never referenced the account.
        (await _host.QueryAsync(db => db.JobSearchJobs.CountAsync())).ShouldBe(2);
    }
}
