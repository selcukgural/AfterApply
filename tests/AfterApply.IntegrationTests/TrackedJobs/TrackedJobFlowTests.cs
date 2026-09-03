using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.TrackedJobs;

[Collection(IntegrationTestCollection.Name)]
public class TrackedJobFlowTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(TrackedJobFlowTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Tracked", "Job", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Create_List_And_Delete_TrackedJob()
    {
        var client = await AuthenticatedClientAsync("tracked.crud@example.com");

        var createResponse = await client.PostAsJsonAsync("/api/tracked-jobs",
            new CreateTrackedJobRequest("Watchlist Co", "Staff Engineer", "https://example.com/jobs/1", "Remote", "Looks promising"),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TrackedJobResponse>(JsonOptions);
        created.ShouldNotBeNull();
        created!.CompanyName.ShouldBe("Watchlist Co");

        var listResponse = await client.GetAsync("/api/tracked-jobs");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<TrackedJobResponse>>(JsonOptions);
        list.ShouldNotBeNull();
        list!.ShouldContain(t => t.Id == created.Id);

        var deleteResponse = await client.DeleteAsync($"/api/tracked-jobs/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listAfterDelete = await client.GetAsync("/api/tracked-jobs");
        var afterDelete = await listAfterDelete.Content.ReadFromJsonAsync<List<TrackedJobResponse>>(JsonOptions);
        afterDelete.ShouldNotBeNull();
        afterDelete!.ShouldNotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Convert_TrackedJob_Creates_Application_And_Removes_TrackedJob()
    {
        var client = await AuthenticatedClientAsync("tracked.convert@example.com");

        var createResponse = await client.PostAsJsonAsync("/api/tracked-jobs",
            new CreateTrackedJobRequest("Convert Co", "Principal Engineer", "https://example.com/jobs/2", null, null),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<TrackedJobResponse>(JsonOptions);
        created.ShouldNotBeNull();

        var convertResponse = await client.PostAsJsonAsync($"/api/tracked-jobs/{created!.Id}/convert",
            new ConvertTrackedJobRequest(EmploymentType.FullTime, DateTimeOffset.UtcNow, "Applied via referral"),
            JsonOptions);
        convertResponse.EnsureSuccessStatusCode();
        var application = await convertResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        application.ShouldNotBeNull();
        application!.CompanyName.ShouldBe("Convert Co");
        application.JobTitle.ShouldBe("Principal Engineer");
        application.Status.ShouldBe(ApplicationStatus.Applied);

        var listResponse = await client.GetAsync("/api/tracked-jobs");
        var list = await listResponse.Content.ReadFromJsonAsync<List<TrackedJobResponse>>(JsonOptions);
        list.ShouldNotBeNull();
        list!.ShouldNotContain(t => t.Id == created.Id);

        var applicationsResponse = await client.GetAsync($"/api/applications/{application.Id}");
        applicationsResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Convert_Unknown_TrackedJob_Returns_NotFound()
    {
        var client = await AuthenticatedClientAsync("tracked.notfound@example.com");

        var response = await client.PostAsJsonAsync($"/api/tracked-jobs/{Guid.NewGuid()}/convert",
            new ConvertTrackedJobRequest(EmploymentType.FullTime, DateTimeOffset.UtcNow, null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveLink_For_Unsupported_Host_Returns_Empty_Suggestions_Not_An_Error()
    {
        var client = await AuthenticatedClientAsync("tracked.resolvelink@example.com");

        var response = await client.PostAsJsonAsync("/api/tracked-jobs/resolve-link",
            new ResolveTrackedJobLinkRequest("https://example.com/some/job/posting"), JsonOptions);

        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<TrackedJobLinkPreviewResponse>(JsonOptions);
        preview.ShouldNotBeNull();
        preview!.SuggestedCompanyName.ShouldBeNull();
        preview.SuggestedJobTitle.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveLink_Requires_Authentication()
    {
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tracked-jobs/resolve-link",
            new ResolveTrackedJobLinkRequest("https://www.linkedin.com/jobs/view/123/"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
