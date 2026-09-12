using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>
/// The country/language rule end to end. The audience is in Türkiye, so country falls back to
/// "tr" through the user's settings to the global default — but language falls back to
/// <b>nothing</b>: JSearch answers a country/language mismatch with an empty list, and an
/// English-titled posting in İstanbul is still a posting in İstanbul. Only a language the user
/// or their settings name is ever sent.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchCountryLanguageTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private JobSearchTestHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchCountryLanguageTests));
        _host.Handler.SearchBody = JSearchFixtures.Read("search-v2-tr.json");
        _client = await _host.RegisterAsync("country.language@example.com");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task With_Nothing_Said_The_Search_Goes_To_Turkey_With_No_Language()
    {
        var response = await _client.GetAsync("/api/job-search/jobs?query=backend%20developer%20istanbul");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var uri = _host.Handler.UrisFor("/search-v2").ShouldHaveSingleItem();
        uri.Query.ShouldBe("?query=backend%20developer%20istanbul&country=tr");
    }

    [Fact]
    public async Task An_English_Titled_Posting_In_Turkey_Is_Returned_Untouched()
    {
        var result = await _client.GetFromJsonAsync<JobSearchResultsResponse>(
            "/api/job-search/jobs?query=backend%20developer%20istanbul", JobSearchTestHost.Json);

        result!.Jobs.Count.ShouldBe(2);
        result.Jobs.Select(j => j.Title).ShouldBe(["Kıdemli Backend Geliştirici", "Senior Backend Engineer (Go)"]);
        result.Jobs.ShouldAllBe(j => j.Country == "TR");
    }

    [Fact]
    public async Task A_Saved_Country_Replaces_The_Global_One_And_The_Request_Beats_Both()
    {
        var saved = await _client.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("nl", null, null, null, null), JobSearchTestHost.Json);
        saved.EnsureSuccessStatusCode();

        (await _client.GetAsync("/api/job-search/jobs?query=backend")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=backend&country=DE")).EnsureSuccessStatusCode();

        var uris = _host.Handler.UrisFor("/search-v2");
        uris[0].Query.ShouldBe("?query=backend&country=nl");
        uris[1].Query.ShouldBe("?query=backend&country=de");
    }

    [Fact]
    public async Task A_Language_Appears_Only_When_The_User_Or_Their_Settings_Name_One()
    {
        (await _client.GetAsync("/api/job-search/jobs?query=backend&language=EN")).EnsureSuccessStatusCode();

        var saved = await _client.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest(null, "tr", null, null, null), JobSearchTestHost.Json);
        saved.EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=frontend")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=frontend&language=en")).EnsureSuccessStatusCode();

        var cleared = await _client.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest(null, null, null, null, null), JobSearchTestHost.Json);
        cleared.EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs?query=devops")).EnsureSuccessStatusCode();

        var uris = _host.Handler.UrisFor("/search-v2");
        uris.Count.ShouldBe(4);
        uris[0].Query.ShouldBe("?query=backend&country=tr&language=en");
        uris[1].Query.ShouldBe("?query=frontend&country=tr&language=tr");
        uris[2].Query.ShouldBe("?query=frontend&country=tr&language=en");
        uris[3].Query.ShouldBe("?query=devops&country=tr");
    }

    [Fact]
    public async Task The_Details_Lookup_Follows_The_Same_Rule()
    {
        (await _client.GetAsync("/api/job-search/jobs/details?ids=TR-Turkish-1")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/job-search/jobs/details?ids=TR-Turkish-1&country=DE&language=de")).EnsureSuccessStatusCode();

        var uris = _host.Handler.UrisFor("/job-details");
        uris[0].Query.ShouldBe("?job_id=TR-Turkish-1&country=tr");
        uris[1].Query.ShouldBe("?job_id=TR-Turkish-1&country=de&language=de");
    }

    [Fact]
    public async Task A_Mismatch_The_Provider_Answers_With_Nothing_Is_An_Empty_List_Not_An_Error()
    {
        _host.Handler.AnswerNext(HttpStatusCode.OK,
            """{"status":"OK","request_id":"empty","parameters":{},"data":{"jobs":[],"cursor":null}}""", 149);

        var response = await _client.GetAsync("/api/job-search/jobs?query=backend&language=xx");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JobSearchResultsResponse>(JobSearchTestHost.Json);
        result!.Jobs.ShouldBeEmpty();
        result.NextCursor.ShouldBeNull();
        result.Meta.CreditsCharged.ShouldBe(1);
    }

    [Fact]
    public async Task A_Malformed_Language_Is_Refused_Before_Anything_Is_Sent()
    {
        var response = await _client.GetAsync("/api/job-search/jobs?query=backend&language=english");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _host.Handler.CallCount.ShouldBe(0);
    }
}
