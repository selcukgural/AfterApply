using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Infrastructure.JobSearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>Off by default, and "off" means the routes do not exist — plus the guard that a
/// developer's live key can never reach a test host.</summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchFlagOffTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private JobSearchTestHost _off = null!;
    private JobSearchTestHost _noKey = null!;

    public async Task InitializeAsync()
    {
        _off = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchFlagOffTests) + "Off", enabled: false, apiKey: "");
        _noKey = await JobSearchTestHost.StartAsync(shared, nameof(JobSearchFlagOffTests) + "NoKey", enabled: true, apiKey: "");
    }

    public async Task DisposeAsync()
    {
        await _off.DisposeAsync();
        await _noKey.DisposeAsync();
    }

    [Theory]
    [InlineData("/api/job-search/jobs?query=x")]
    [InlineData("/api/job-search/jobs/details?ids=x")]
    [InlineData("/api/job-search/salary?jobTitle=x&location=y")]
    [InlineData("/api/job-search/company-salary?company=x&jobTitle=y")]
    [InlineData("/api/job-search/usage")]
    [InlineData("/api/job-search/settings")]
    public async Task Every_Route_Is_404_While_The_Flag_Is_Off(string path)
    {
        var client = await _off.RegisterAsync($"off.{Guid.NewGuid():N}@example.com");

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _off.Handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_Admin_Route_Is_404_Too()
    {
        const string email = "off.admin@ekariyerim.com";
        var admin = await _off.RegisterAsync(email);
        await _off.SetAdminAsync(email);

        var response = await admin.GetAsync($"/api/admin/job-search/settings/{await _off.UserIdAsync(email)}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Config_Says_Disabled()
    {
        var config = await _off.Factory.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/config");

        config.GetProperty("jobSearch").GetProperty("enabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Flag_On_With_No_Key_Reads_As_Unavailable_Rather_Than_Missing()
    {
        var client = await _noKey.RegisterAsync("nokey@example.com");

        var response = await client.GetAsync("/api/job-search/jobs?query=x");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("Job search is not available right now.");
        _noKey.Handler.CallCount.ShouldBe(0);

        var config = await _noKey.Factory.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/config");
        config.GetProperty("jobSearch").GetProperty("enabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Settings_Still_Work_Without_A_Key_So_A_User_Can_Prepare()
    {
        var client = await _noKey.RegisterAsync("nokey.settings@example.com");

        var response = await client.PutAsJsonAsync("/api/job-search/settings",
            new UpdateJobSearchPreferencesRequest("tr", null, null, null, null), JobSearchTestHost.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

/// <summary>
/// The leak guard, on its own host with nothing configured: whatever a developer keeps in user
/// secrets, a test host sees the feature off and the key empty — the environment variables set
/// by TestContainerCleanup.DisableJobSearchForTests sit above user secrets. This is the exact
/// incident DisableFeedbackMirrorForTests documents, prevented before it happens here.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSearchLeakGuardTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(JobSearchLeakGuardTests));
        _factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));
        });
    }

    public async Task DisposeAsync() => await TestHostDisposal.DisposeQuietlyAsync(_factory);

    [Fact]
    public void A_Host_That_Configures_Nothing_Has_The_Feature_Off_And_No_Key()
    {
        var options = _factory!.Services.GetRequiredService<IOptions<JobSearchOptions>>().Value;

        options.Enabled.ShouldBeFalse();
        options.ApiKey.ShouldBeNullOrEmpty();
        options.IsConfigured.ShouldBeFalse();
    }
}
