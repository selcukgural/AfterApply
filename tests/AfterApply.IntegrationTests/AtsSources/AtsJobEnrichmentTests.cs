using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.IntegrationTests.Companies;
using AfterApply.Infrastructure.AtsSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.AtsSources;

// Covers the background job the extension's "I Applied" action queues for an ATS-hosted posting:
// the description the page scrape could not reach is read back from that ATS's own public API.
// Only the transport is stubbed — the real URL builder (the SSRF boundary), the real host
// allow-list, the real parser and the real fill-if-missing rule all run.
public sealed class AtsEnrichmentProfile : IHostProfile
{
    public StubHttpMessageHandler Handler { get; } = new(new Dictionary<string, string>
    {
        ["greenhouse.io"] = AtsJobEnrichmentTests.GreenhouseJson,
        ["workable.com"] = AtsJobEnrichmentTests.WorkableBoardJson,
    });

    public void Configure(IWebHostBuilder builder)
    {
        // AtsSources ships off, as every outbound-fetch flag does. Turned on here so the path
        // under test actually runs; AtsJobEnrichmentFlagOffTests covers the shipped default.
        builder.UseSetting("AtsSources:Enabled", "true");

        // The typed client's name is the interface's short name, so re-registering it appends to
        // the same named options and replaces the primary handler.
        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(IAtsJobClient))
                .ConfigurePrimaryHttpMessageHandler(() => Handler));
    }

    public void Reset() => Handler.Clear();
}

[Collection(IntegrationTestCollection.Name)]
public class AtsJobEnrichmentTests(ApiHost<AtsEnrichmentProfile> host) : IClassFixture<ApiHost<AtsEnrichmentProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    // Trimmed from a real boards-api.greenhouse.io response (2026-09-22). "content" is
    // entity-escaped HTML, which is Greenhouse's own doing and what the parser undoes.
    internal const string GreenhouseJson = """
        {"id":"4512345","title":"Abuse Investigator","location":{"name":"Seattle, San Francisco"},
         "first_published":"2026-09-09T10:50:29-04:00",
         "content":"&lt;p&gt;You will investigate abuse across the payments network, working with risk and legal.&lt;/p&gt;&lt;ul&gt;&lt;li&gt;Five years of experience&lt;/li&gt;&lt;li&gt;Strong written communication&lt;/li&gt;&lt;/ul&gt;"}
        """;

    // Trimmed from a real apply.workable.com widget response (2026-09-22). The board, not a single
    // posting — Workable publishes no per-job endpoint, so the row is picked out by shortcode, the
    // same shape Ashby needs. Without "?details=true" these rows carry no description at all,
    // which is the reading that made 0.9.0 skip Workable entirely.
    internal const string WorkableBoardJson = """
        {"name":"acme","jobs":[
          {"shortcode":"9999999999","title":"Another Role","city":"Berlin","country":"Germany"},
          {"shortcode":"A1B2C3D4E5","title":"Client Experience Coordinator",
           "description":"<h3>About the role</h3><p>You will coordinate the client experience team.</p>",
           "city":"Athens","state":"Attica","country":"Greece",
           "employment_type":"Full-time","published_on":"2026-02-12"}]}
        """;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;
    private StubHttpMessageHandler _handler => host.Profile.Handler;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, _factory.Services,
            new RegisterRequest("ats.enrichment@example.com", "P@ssw0rd123!", "Ats", "Test", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Posting_Captured_Without_A_Description_Gets_One_From_The_Ats_Api()
    {
        var job = await CaptureAsync("https://job-boards.greenhouse.io/stripe/jobs/4512345", description: null);

        // Exactly the address AtsApiUrlBuilder documents — asserted here because this is the one
        // URL the fetch is allowed to request.
        _handler.Requested.ShouldContain(uri =>
            uri.ToString() == "https://boards-api.greenhouse.io/v1/boards/stripe/jobs/4512345?content=true");

        job.Description.ShouldNotBeNull();
        job.Description.ShouldContain("investigate abuse across the payments network");
        job.DescriptionHtml.ShouldContain("<li>Five years of experience</li>");
        job.Location.ShouldBe("Seattle, San Francisco");
        job.PublishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Posting_The_Extension_Already_Read_In_Full_Is_Left_Alone()
    {
        // The scrape is the primary source: the user was looking at that page. Re-reading a
        // posting we already have buys nothing and costs the ATS a request.
        var scraped = new string('x', 500);

        var job = await CaptureAsync("https://job-boards.greenhouse.io/stripe/jobs/4512346", scraped);

        _handler.Requested.ShouldBeEmpty();
        job.Description.ShouldBe(scraped);
    }

    [Fact]
    public async Task A_Stub_Length_Description_Is_Treated_As_Missing()
    {
        // "Apply on the company site" and friends: present, but not something CV scanning or
        // job-fit scoring can work with.
        var job = await CaptureAsync("https://job-boards.greenhouse.io/stripe/jobs/4512347", "Apply on our careers page.");

        // Fill-if-missing, so the short original wins the Description field...
        job.Description.ShouldBe("Apply on our careers page.");
        // ...but the fields it never had are filled from the API, which is the point.
        _handler.Requested.ShouldNotBeEmpty();
        job.Location.ShouldBe("Seattle, San Francisco");
        job.DescriptionHtml.ShouldContain("Five years of experience");
    }

    [Fact]
    public async Task Nothing_Is_Fetched_For_A_Posting_That_Is_Not_On_An_Ats()
    {
        var job = await CaptureAsync("https://www.linkedin.com/jobs/view/4449445627/", description: null);

        host.Jobs.Pending.ShouldBeEmpty();
        _handler.Requested.ShouldBeEmpty();
        job.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Workable_Is_Read_Out_Of_The_Board_It_Publishes()
    {
        // 0.9.0 skipped Workable on the reading that its widget endpoint returns the company and
        // not the posting. It does — until "?details=true", which puts each posting's description
        // in the rows (DECISIONS.md 2026-09-22, "Workable'ın ucu meğer varmış").
        var job = await CaptureAsync("https://apply.workable.com/acme/j/A1B2C3D4E5", description: null);

        _handler.Requested.ShouldContain(uri => uri.Query.Contains("details=true", StringComparison.Ordinal));
        // The title the extension captured stands — enrichment is fill-if-missing and never
        // rewrites a field the page already answered. What it fills is what was empty.
        job.Title.ShouldBe("Abuse Investigator");
        job.Description.ShouldContain("coordinate the client experience team");
        job.DescriptionHtml.ShouldContain("<h3>About the role</h3>");
        // The city carries the meaning; the region is dropped rather than repeated.
        job.Location.ShouldBe("Athens, Greece");
    }

    [Fact]
    public async Task A_Workable_Board_That_No_Longer_Lists_The_Posting_Leaves_The_Row_Alone()
    {
        // A filled or unpublished job: the board answers, the shortcode is not in it, and nothing
        // is written — rather than the first row's fields landing on someone else's application.
        var job = await CaptureAsync("https://apply.workable.com/acme/j/DEADBEEF99", description: null);

        _handler.Requested.ShouldNotBeEmpty();
        job.Title.ShouldBe("Abuse Investigator");
        job.Description.ShouldBeNull();
    }

    [Fact]
    public async Task An_Ats_That_Answers_With_Nothing_Useful_Leaves_The_Row_As_It_Was()
    {
        // The stub 404s any host it has no canned body for; lever.co is not in its table.
        var job = await CaptureAsync("https://jobs.lever.co/acme/8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f", description: null);

        _handler.Requested.ShouldNotBeEmpty();
        job.Description.ShouldBeNull();
        job.Location.ShouldBeNull();
    }

    private async Task<Domain.Jobs.Job> CaptureAsync(string jobUrl, string? description)
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Stripe", "Abuse Investigator", jobUrl,
                Location: null, description, PublishedAt: null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(a => a.Id == created!.Application.Id);
        return await db.Jobs.SingleAsync(j => j.Id == application.JobId);
    }
}

/// <summary>The shipped default: AtsSources off. Mirrors JobSourceFlagOffTests — a feature that is
/// off must not reach the network even for a posting that would otherwise qualify.</summary>
public sealed class AtsEnrichmentFlagOffProfile : IHostProfile
{
    public StubHttpMessageHandler Handler { get; } = new(new Dictionary<string, string>
    {
        ["greenhouse.io"] = AtsJobEnrichmentTests.GreenhouseJson,
    });

    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(IAtsJobClient))
                .ConfigurePrimaryHttpMessageHandler(() => Handler));

    public void Reset() => Handler.Clear();
}

[Collection(IntegrationTestCollection.Name)]
public class AtsJobEnrichmentFlagOffTests(ApiHost<AtsEnrichmentFlagOffProfile> host)
    : IClassFixture<ApiHost<AtsEnrichmentFlagOffProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = ((WebApplicationFactory<Program>)host).CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, ((WebApplicationFactory<Program>)host).Services,
            new RegisterRequest("ats.flagoff@example.com", "P@ssw0rd123!", "Ats", "Off", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task With_The_Flag_Off_The_Job_Runs_And_Fetches_Nothing()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Stripe", "Abuse Investigator",
                "https://job-boards.greenhouse.io/stripe/jobs/4512345",
                Location: null, Description: null, PublishedAt: null),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        // The job is still enqueued — the flag is checked inside it, so that a job already on the
        // queue when the flag goes off does not fetch anyway.
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();

        host.Profile.Handler.Requested.ShouldBeEmpty();
    }
}
