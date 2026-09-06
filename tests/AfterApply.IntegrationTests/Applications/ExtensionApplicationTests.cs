using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

// Covers POST /api/applications/from-extension (Sprint 9's "I Applied" action): Job/Company
// resolution via the existing resolvers, the FullTime EmploymentType default (same known
// limitation as generic CSV import, DECISIONS.md Sprint 4), and same-JobUrl dedup returning the
// existing row instead of creating a duplicate when the button is effectively clicked twice.
[Collection(IntegrationTestCollection.Name)]
public class ExtensionApplicationTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(ExtensionApplicationTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });


        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("extension.test@example.com", "P@ssw0rd123!", "Extension", "Test", true), JsonOptions);
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
    public async Task Create_Resolves_Company_And_Sets_BrowserExtension_Source_And_FullTime_Default()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Acme Corp", "Backend Engineer",
                "https://www.linkedin.com/jobs/view/4449445627/", "Istanbul", "We build things.", DateTimeOffset.UtcNow.AddDays(-1)),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        result!.WasDuplicate.ShouldBeFalse();
        result.Application.CompanyName.ShouldBe("Acme Corp");
        result.Application.JobTitle.ShouldBe("Backend Engineer");
        result.Application.Source.ShouldBe(Source.BrowserExtension);
        result.Application.EmploymentType.ShouldBe(EmploymentType.FullTime);
    }

    [Fact]
    public async Task Create_From_KariyerNet_Url_Sets_KariyerNet_Job_Source()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Dolusoft", "Yazılım Destek Uzmanı",
                "https://www.kariyer.net/is-ilani/dolusoft-yazilim-teknolojileri-limited-sirketi-yazilim-destek-uzmani-4539310",
                "Ankara", "Uzaktan teknik destek.", DateTimeOffset.UtcNow.AddDays(-1)),
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        result!.WasDuplicate.ShouldBeFalse();
        result.Application.Source.ShouldBe(Source.BrowserExtension);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(a => a.Id == result.Application.Id);
        var job = await db.Jobs.SingleAsync(j => j.Id == application.JobId);
        job.Source.ShouldBe(Source.KariyerNet);
        job.ExternalId.ShouldBe("4539310");
    }

    [Fact]
    public async Task Create_With_Same_JobUrl_Twice_Returns_Existing_Application_As_Duplicate()
    {
        var request = new CreateFromExtensionRequest("Acme Corp", "Backend Engineer",
            "https://www.linkedin.com/jobs/view/4449445627/", "Istanbul", null, null);

        var firstResponse = await _client.PostAsJsonAsync("/api/applications/from-extension", request, JsonOptions);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        first!.WasDuplicate.ShouldBeFalse();

        var secondResponse = await _client.PostAsJsonAsync("/api/applications/from-extension", request, JsonOptions);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        second!.WasDuplicate.ShouldBeTrue();
        second.Application.Id.ShouldBe(first.Application.Id);

        var listResponse = await _client.GetAsync("/api/applications");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions);
        list!.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Create_With_High_Confidence_Fuzzy_Match_Attaches_To_Existing_Company()
    {
        // Seeded manually (not via extension) so the two applications don't collide on JobUrl
        // dedup — this test is specifically about company resolution, not the JobUrl dedup path
        // already covered above.
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Nova Yazilim", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        // A single-character typo of the seeded name — a near-duplicate, not a genuinely new
        // company — clears the default 0.75 trigram similarity threshold.
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Nova Yazlim", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/1111111111/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldBe(seeded!.CompanyId);
    }

    // Company.Website/LinkedInUrl are filled by CompanyEnrichmentService in the background, well
    // after the application row exists — so the detail response has to read them from the Company
    // at response time. If they were ever snapshotted onto the Application at creation, this test
    // would see nulls.
    [Fact]
    public async Task Detail_Surfaces_Company_Links_Filled_In_After_The_Application_Was_Created()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Enriched Labs", "Platform Engineer",
                "https://www.linkedin.com/jobs/view/3333333333/", "Remote", null, null),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        // Nothing has enriched this company yet.
        created!.Application.CompanyWebsite.ShouldBeNull();

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.SingleAsync(c => c.Id == created.Application.CompanyId);
            company.EnrichFrom("https://enrichedlabs.example", "Software Development", "TR", DateTimeOffset.UtcNow);
            company.SetProfileLinksIfMissing("https://www.linkedin.com/company/enriched-labs/", kariyerNetUrl: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var detailResponse = await _client.GetAsync($"/api/applications/{created.Application.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        detail!.CompanyWebsite.ShouldBe("https://enrichedlabs.example");
        detail.CompanyLinkedInUrl.ShouldBe("https://www.linkedin.com/company/enriched-labs/");
        detail.CompanyName.ShouldBe("Enriched Labs");
    }

    // Extension updates are not something we can force: a user can stay on an old build
    // indefinitely (Chrome Web Store review alone can take days), so every field the newer builds
    // added has to be optional on the wire, not just in the C# signature. This posts the exact JSON
    // body extension 0.5.0 sends — captured from that build's popup.js, not paraphrased — so the
    // day someone makes one of these fields required, this test says so instead of a user's popup
    // silently failing.
    [Fact]
    public async Task A_Body_From_Extension_0_5_0_Still_Creates_An_Application()
    {
        const string legacyBody = """
            {
              "companyName": "Legacy Ext Corp",
              "jobTitle": "Backend Engineer",
              "jobUrl": "https://www.linkedin.com/jobs/view/4400000001/",
              "location": "Istanbul",
              "description": "We build things.",
              "descriptionHtml": "<p>We build things.</p>",
              "publishedAt": null,
              "companyLinkedInUrl": "https://www.linkedin.com/company/legacy-ext-corp/"
            }
            """;

        var response = await _client.PostAsync("/api/applications/from-extension",
            new StringContent(legacyBody, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);
        result!.Application.CompanyName.ShouldBe("Legacy Ext Corp");
        result.Application.Source.ShouldBe(Source.BrowserExtension);
        // Everything the newer builds added simply stays empty.
        result.Application.HrName.ShouldBeNull();
        result.Application.HrEmail.ShouldBeNull();
        result.Application.HrLinkedInUrl.ShouldBeNull();
    }

    // The other direction: a newer extension can reach a backend that has not been redeployed yet.
    // Unknown properties must be ignored rather than rejected, or the rollout order would become
    // load-bearing.
    [Fact]
    public async Task A_Body_Carrying_Fields_This_Backend_Does_Not_Know_Is_Still_Accepted()
    {
        const string futureBody = """
            {
              "companyName": "Future Ext Corp",
              "jobTitle": "Backend Engineer",
              "jobUrl": "https://www.linkedin.com/jobs/view/4400000002/",
              "somethingAddedLater": "whatever",
              "hrPhoneNumber": "+90 555 000 0000"
            }
            """;

        var response = await _client.PostAsync("/api/applications/from-extension",
            new StringContent(futureBody, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // The extension only ever sends this when LinkedIn's hiring-team card was actually present —
    // most postings have none, and kariyer.net never does.
    [Fact]
    public async Task Extension_Submission_Carries_The_Job_Posters_Profile_Onto_The_Application()
    {
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Hirer Corp", "Senior Developer",
                "https://www.linkedin.com/jobs/view/4442825208/", "Istanbul", null, null, null, null, null,
                HrName: "Çiğdem Ç.", HrEmail: null,
                HrLinkedInUrl: "https://www.linkedin.com/in/cigdem-kara-b8194077/"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.HrName.ShouldBe("Çiğdem Ç.");
        result.Application.HrLinkedInUrl.ShouldBe("https://www.linkedin.com/in/cigdem-kara-b8194077/");
        result.Application.HrEmail.ShouldBeNull();
    }

    [Fact]
    public async Task Extension_Submission_With_A_Company_Page_As_The_Hr_Profile_Is_Rejected()
    {
        // Guards the selector's whole point: the hiring-team card is a person, and a company page
        // (or an unrelated profile scraped from elsewhere on the job page) must not pass as one.
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Wrong Hirer Corp", "Senior Developer",
                "https://www.linkedin.com/jobs/view/4442825209/", null, null, null, null, null, null,
                HrLinkedInUrl: "https://www.linkedin.com/company/turkcell/"),
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Hr_Contact_Round_Trips_Through_Create_Update_And_Detail()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Contact Corp", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null,
            HrName: "Zeynep A.", HrEmail: "talent@contactcorp.example",
            HrLinkedInUrl: "https://www.linkedin.com/in/zeynep-a"), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        created!.HrEmail.ShouldBe("talent@contactcorp.example");

        // Clearing is a real edit, not a no-op: UpdateDetails assigns straight through so a user
        // who empties the field gets rid of a stale contact.
        var updateResponse = await _client.PutAsJsonAsync($"/api/applications/{created.Id}", new UpdateApplicationRequest(
            created.JobTitle, null, null, EmploymentType.FullTime, created.AppliedAt, null,
            HrName: "Mehmet B.", HrEmail: null, HrLinkedInUrl: null), JsonOptions);
        updateResponse.EnsureSuccessStatusCode();

        var detailResponse = await _client.GetAsync($"/api/applications/{created.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        detail!.HrName.ShouldBe("Mehmet B.");
        detail.HrEmail.ShouldBeNull();
        detail.HrLinkedInUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Create_With_Low_Confidence_Match_Creates_New_Company()
    {
        var seedResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Zeta Robotics", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null),
            JsonOptions);
        seedResponse.EnsureSuccessStatusCode();
        var seeded = await seedResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Delta Analytics", "Frontend Engineer",
                "https://www.linkedin.com/jobs/view/2222222222/", null, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions);

        result!.Application.CompanyId.ShouldNotBe(seeded!.CompanyId);
    }
}
