using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Medallion.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Companies;

// Covers the background company enrichment that the extension's "I Applied" action queues: the
// job posting carries a link to the company's own profile page, and that page is fetched to fill
// in Website/Industry/Country. The kariyer.net branch is what this class is mostly about — before
// it existed, a company first seen in a kariyer.net posting had no profile URL at all and was
// never enriched.
//
// The HTTP transport is stubbed (see StubHttpMessageHandler) but everything else is real: the
// job hand-off, the host allow-list, and both parsers.
public sealed class CompanyEnrichmentProfile : IHostProfile
{
    public StubHttpMessageHandler Handler { get; } = new(new Dictionary<string, string>
    {
        ["kariyer.net"] = CompanyEnrichmentTests.KariyerNetProfileHtml,
        ["linkedin.com"] = CompanyEnrichmentTests.LinkedInProfileHtml,
    });

    public void Configure(IWebHostBuilder builder)
    {
        // The typed client's name is the interface's short name, so re-registering it here
        // appends to the same named options and replaces the primary handler — no need to
        // reach for the internal implementation type.
        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(ICompanyEnrichmentService))
                .ConfigurePrimaryHttpMessageHandler(() => Handler));
    }

    public void Reset() => Handler.Clear();
}

[Collection(IntegrationTestCollection.Name)]
public class CompanyEnrichmentTests(ApiHost<CompanyEnrichmentProfile> host) : IClassFixture<ApiHost<CompanyEnrichmentProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    // Trimmed to the shapes the parsers actually read — see KariyerNetCompanyProfileParserTests
    // for where these came from and why the payload is index-encoded.
    internal const string KariyerNetProfileHtml = """
        <html><body>
        <h1 class="text-lg font-medium">Acme Lojistik</h1>
        <div class="flex gap-2">
          <a href="https://www.acme-lojistik.com.tr" title="Acme Lojistik Web Sitesi" rel="nofollow" target="_blank"></a>
        </div>
        <script type="application/json" id="__NUXT_DATA__">[
        {"id":1,"companySectors":2,"companyInfo":4},
        56971,
        [3],
        {"sectorCode":6,"sectorName":5,"isDefaultSector":7},
        {"workerCount":8,"foundationYear":8},
        "Lojistik",
        "45",
        false,
        "-"
        ]</script>
        </body></html>
        """;

    internal const string LinkedInProfileHtml = """
        <html><body>
        <dt>Website</dt>
        <dd><a href="https://www.linkedin.com/redir/redirect?url=https%3A%2F%2Facme%2Eexample%2F&amp;urlhash=x">acme.example</a></dd>
        <dt>Industry</dt>
        <dd>Software Development</dd>
        <script type="application/ld+json">{"@type":"Organization","name":"Acme Software","address":{"addressCountry":"TR"}}</script>
        </body></html>
        """;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;
    private StubHttpMessageHandler _handler => host.Profile.Handler;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(_client, _factory.Services,
            new RegisterRequest("company.enrichment@example.com", "P@ssw0rd123!", "Enrich", "Test", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task KariyerNet_Posting_Enriches_The_Company_From_Its_Firma_Profil_Page()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Lojistik", "Yazılım Uzmanı",
                "https://www.kariyer.net/is-ilani/acme-lojistik-yazilim-uzmani-4545233",
                "Bursa", null, null, null, null,
                CompanyKariyerNetUrl: "https://www.kariyer.net/firma-profil/acme-lojistik-1166-1810"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        var company = await PollUntilEnrichedAsync(created!.Application.CompanyId);

        company.KariyerNetUrl.ShouldBe("https://www.kariyer.net/firma-profil/acme-lojistik-1166-1810");
        company.Website.ShouldBe("https://www.acme-lojistik.com.tr");
        company.Industry.ShouldBe("Lojistik");
        // kariyer.net publishes no country anywhere on the profile.
        company.Country.ShouldBeNull();

        _handler.Requested.ShouldContain(uri => uri.Host == "www.kariyer.net");
        _handler.Requested.ShouldNotContain(uri => uri.Host.EndsWith("linkedin.com"));
    }

    [Fact]
    public async Task LinkedIn_Posting_Still_Enriches_From_The_LinkedIn_Company_Page()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Software", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/4444444444/", "Istanbul", null, null, null,
                CompanyLinkedInUrl: "https://www.linkedin.com/company/acme-software/"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        var company = await PollUntilEnrichedAsync(created!.Application.CompanyId);

        company.Website.ShouldBe("https://acme.example/");
        company.Industry.ShouldBe("Software Development");
        company.Country.ShouldBe("TR");
    }

    [Fact]
    public async Task Enriched_Company_Fields_Reach_The_Application_Detail_Response()
    {
        // Everything above asserts against the Company row directly. This one closes the loop the
        // enrichment exists for: until 2026-09-07 Industry, Country and KariyerNetUrl were written
        // and then reached no response at all, so the work was invisible (DEVELOPMENT_PLAN.md, K4).
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Software Teknoloji", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/6666666666/", "Istanbul", null, null, null,
                CompanyLinkedInUrl: "https://www.linkedin.com/company/acme-software/"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        await PollUntilEnrichedAsync(created!.Application.CompanyId);

        var detailResponse = await _client.GetAsync($"/api/applications/{created.Application.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        detail.ShouldNotBeNull();
        detail!.CompanyIndustry.ShouldBe("Software Development");
        detail.CompanyCountry.ShouldBe("TR");
        detail.CompanyWebsite.ShouldBe("https://acme.example/");
        detail.CompanyLinkedInUrl.ShouldBe("https://www.linkedin.com/company/acme-software/");
    }

    [Fact]
    public async Task A_Posting_With_No_Profile_Link_Fetches_Nothing_At_All()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Unlinked Co", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/5555555555/", null, null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        // Nothing must have been queued — and if something was, it runs now and the assertions
        // below catch what it did.
        host.Jobs.Pending.ShouldBeEmpty();
        await host.RunJobsAsync();

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await db.Companies.SingleAsync(c => c.Id == created!.Application.CompanyId);

        company.Website.ShouldBeNull();
        company.LinkedInUrl.ShouldBeNull();
        company.KariyerNetUrl.ShouldBeNull();
        _handler.Requested.ShouldBeEmpty();
    }

    /// <summary>Two applications for a new company at the same moment enqueue two enrichments,
    /// and with several instances they run on two workers at once. The lock in Redis lets one of
    /// them do the fetch; the other finds the lock held and leaves without touching the network.
    /// Here the "other worker" is simulated by holding the lock from the test.</summary>
    [Fact]
    public async Task A_Worker_That_Finds_The_Enrichment_Lock_Held_Fetches_Nothing()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Locked Co", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/6666666666/", null, null, null, null,
                CompanyLinkedInUrl: "https://www.linkedin.com/company/locked-co/"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var companyId = (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!.Application.CompanyId;

        var locks = _factory.Services.GetRequiredService<IDistributedLockProvider>();
        var lockName = _factory.Services.GetRequiredService<DistributedLockNames>().CompanyEnrichment(companyId);

        await using (await locks.AcquireLockAsync(lockName))
        {
            await host.RunJobsAsync();
            host.Jobs.Failed.ShouldBeEmpty();
            _handler.Requested.ShouldBeEmpty("the lock was held by 'another worker', so this one must not fetch");
        }

        // Released: the next run does the work.
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICompanyEnrichmentService>().EnrichAsync(companyId, CancellationToken.None);
        _handler.Requested.ShouldContain(uri => uri.Host.EndsWith("linkedin.com"));
    }

    // The page's company name is checked before anything on it is used (2026-09-24): a capture
    // that points a company at somebody else's page fills nothing in, and loses the link, so a
    // later capture pointing at the right page can take the slot.
    [Fact]
    public async Task A_Profile_Page_Naming_Another_Company_Fills_Nothing_And_Loses_Its_Link()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Globex Holding", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/7777777777/", "Istanbul", null, null, null,
                CompanyLinkedInUrl: "https://www.linkedin.com/company/acme-software/"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();

        var company = await host.WithDbAsync(db => db.Companies.SingleAsync(c => c.Id == created!.Application.CompanyId));
        _handler.Requested.ShouldContain(uri => uri.Host == "www.linkedin.com");
        company.Website.ShouldBeNull();
        company.Industry.ShouldBeNull();
        company.Country.ShouldBeNull();
        company.LinkedInUrl.ShouldBeNull();
    }

    // One user's capture fills the website in, and that user sees it on their own application. The
    // public page shows it only once two different people pointed the company at the same page.
    [Fact]
    public async Task The_Website_Is_Public_Only_Once_Two_People_Point_At_The_Same_Page()
    {
        const string companyLink = "https://www.linkedin.com/company/acme-software/";
        var first = await CaptureAsync(_client, "https://www.linkedin.com/jobs/view/8000000001/", companyLink);
        var company = await PollUntilEnrichedAsync(first.Application.CompanyId);
        first.Application.CompanyId.ShouldBe(company.Id);

        // Three different applicants make the company listed, so it has a public page at all.
        var (second, _) = await host.RegisterAsync("second.applicant@example.com");
        var (third, _) = await host.RegisterAsync("third.applicant@example.com");
        await CaptureAsync(second, "https://www.linkedin.com/jobs/view/8000000002/", companyLink: null);
        await CaptureAsync(third, "https://www.linkedin.com/jobs/view/8000000003/", companyLink: null);

        var detail = await (await _client.GetAsync($"/api/applications/{first.Application.Id}"))
            .Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        detail!.CompanyWebsite.ShouldBe("https://acme.example/");
        (await PublicWebsiteAsync(company.Slug!)).ShouldBeNull();

        // The second person points at the same page — through a tracking link, spelled differently.
        await CaptureAsync(second, "https://www.linkedin.com/jobs/view/8000000004/", "https://linkedin.com/company/Acme-Software?trk=x");
        await host.RunJobsAsync();

        (await PublicWebsiteAsync(company.Slug!)).ShouldBe("https://acme.example/");
    }

    private async Task<ExtensionApplicationResponse> CaptureAsync(HttpClient client, string jobUrl, string? companyLink)
    {
        var response = await client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Software", "Backend Engineer", jobUrl, "Istanbul", null, null, null,
                CompanyLinkedInUrl: companyLink),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!;
    }

    private async Task<string?> PublicWebsiteAsync(string slug)
    {
        using var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/companies/public/{slug}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("website").GetString();
    }

    // The enrichment runs out of band as a background job; it runs here, inline, and the row is
    // read back once.
    private async Task<Domain.Companies.Company> PollUntilEnrichedAsync(Guid companyId)
    {
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        company.Website.ShouldNotBeNull("the enrichment job ran but filled nothing in");
        return company;
    }
}
