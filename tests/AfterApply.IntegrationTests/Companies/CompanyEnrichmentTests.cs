using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
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
// Hangfire hand-off, the host allow-list, and both parsers.
[Collection(IntegrationTestCollection.Name)]
public class CompanyEnrichmentTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Trimmed to the shapes the parsers actually read — see KariyerNetCompanyProfileParserTests
    // for where these came from and why the payload is index-encoded.
    private const string KariyerNetProfileHtml = """
        <html><body>
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

    private const string LinkedInProfileHtml = """
        <html><body>
        <dt>Website</dt>
        <dd><a href="https://www.linkedin.com/redir/redirect?url=https%3A%2F%2Facme%2Eexample%2F&amp;urlhash=x">acme.example</a></dd>
        <dt>Industry</dt>
        <dd>Software Development</dd>
        <script type="application/ld+json">{"@type":"Organization","address":{"addressCountry":"TR"}}</script>
        </body></html>
        """;

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private StubHttpMessageHandler _handler = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CompanyEnrichmentTests));

        _handler = new StubHttpMessageHandler(new Dictionary<string, string>
        {
            ["kariyer.net"] = KariyerNetProfileHtml,
            ["linkedin.com"] = LinkedInProfileHtml,
        });

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

            // The typed client's name is the interface's short name, so re-registering it here
            // appends to the same named options and replaces the primary handler — no need to
            // reach for the internal implementation type.
            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(ICompanyEnrichmentService))
                    .ConfigurePrimaryHttpMessageHandler(() => _handler));
        });

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("company.enrichment@example.com", "P@ssw0rd123!", "Enrich", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

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
    public async Task A_Posting_With_No_Profile_Link_Fetches_Nothing_At_All()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Unlinked Co", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/5555555555/", null, null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        // Nothing is queued, so there is no completion to wait for — give any (incorrectly)
        // queued job a fair chance to run before asserting that it did not.
        await Task.Delay(2000);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await db.Companies.SingleAsync(c => c.Id == created!.Application.CompanyId);

        company.Website.ShouldBeNull();
        company.LinkedInUrl.ShouldBeNull();
        company.KariyerNetUrl.ShouldBeNull();
        _handler.Requested.ShouldBeEmpty();
    }

    // Hangfire runs the enrichment out of band, so the row is read back until the job lands. The
    // 60s ceiling matches CsvImportTests' polling helper and exists for the same reason: under
    // concurrent test-class load a trivial job can take far longer than it would in isolation, and
    // a too-tight timeout risks the fixture tearing down containers under a still-running job.
    private async Task<Domain.Companies.Company> PollUntilEnrichedAsync(Guid companyId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            using (var scope = _factory!.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var company = await db.Companies.SingleAsync(c => c.Id == companyId);
                if (company.Website is not null)
                {
                    return company;
                }
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Company {companyId} was not enriched within 60s.");
    }
}
