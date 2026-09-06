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
public class StatusHistoryTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(StatusHistoryTests));

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

    private async Task<HttpClient> RegisterAndAuthenticateAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "History", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<ApplicationDetailResponse> CreateApplicationAsync(HttpClient client, string company)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Backend Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-3), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
    }

    private static async Task<List<ApplicationStatusHistoryResponse>> GetHistoryAsync(HttpClient client, Guid applicationId)
    {
        var response = await client.GetAsync($"/api/applications/{applicationId}/status-history");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ApplicationStatusHistoryResponse>>(JsonOptions))!;
    }

    [Fact]
    public async Task Status_History_Returns_The_Note_The_User_Wrote()
    {
        // The note used to be written and never read back by anything except the data export —
        // this is the endpoint that makes it visible.
        var client = await RegisterAndAuthenticateAsync("history.note@example.com");
        var created = await CreateApplicationAsync(client, "Note Co");

        var statusResponse = await client.PostAsJsonAsync($"/api/applications/{created.Id}/status",
            new ChangeStatusRequest(ApplicationStatus.Screening, "Recruiter reached out", null), JsonOptions);
        statusResponse.EnsureSuccessStatusCode();

        var history = await GetHistoryAsync(client, created.Id);

        var latest = history[0];
        latest.Note.ShouldBe("Recruiter reached out");
        latest.FromStatus.ShouldBe(ApplicationStatus.Applied);
        latest.ToStatus.ShouldBe(ApplicationStatus.Screening);
        latest.Origin.ShouldBe(StatusChangeOrigin.Manual);
    }

    [Fact]
    public async Task Status_History_Is_Newest_First_And_Includes_The_Seed_Row()
    {
        var client = await RegisterAndAuthenticateAsync("history.order@example.com");
        var created = await CreateApplicationAsync(client, "Order Co");

        foreach (var status in new[] { ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.Rejected })
        {
            var response = await client.PostAsJsonAsync($"/api/applications/{created.Id}/status",
                new ChangeStatusRequest(status, null, null), JsonOptions);
            response.EnsureSuccessStatusCode();
        }

        var history = await GetHistoryAsync(client, created.Id);

        history.Count.ShouldBe(4);
        history.Select(h => h.ToStatus).ShouldBe([
            ApplicationStatus.Rejected,
            ApplicationStatus.Interview,
            ApplicationStatus.Screening,
            ApplicationStatus.Applied
        ]);

        // The creation row has no "from" — it is where the history starts, not a transition.
        history[^1].FromStatus.ShouldBeNull();
        history[^1].Origin.ShouldBe(StatusChangeOrigin.Manual);
    }

    [Fact]
    public async Task Status_Change_Through_The_Api_Is_Always_Recorded_As_Manual()
    {
        // Provenance is decided by the code path, not the caller. ChangeStatusRequest has no Source
        // or Origin field at all, so even a request that tries to look email-driven — by sending
        // fields the contract doesn't declare — is still recorded as a manual change.
        var client = await RegisterAndAuthenticateAsync("history.spoof@example.com");
        var created = await CreateApplicationAsync(client, "Spoof Co");

        using var content = new StringContent(
            """{"newStatus":"Rejected","note":"mine","changedAt":null,"source":"Email","origin":"EmailAutoApplied"}""",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/api/applications/{created.Id}/status", content);
        response.EnsureSuccessStatusCode();

        var latest = (await GetHistoryAsync(client, created.Id))[0];
        latest.Origin.ShouldBe(StatusChangeOrigin.Manual);
        latest.Source.ShouldBe(Source.Manual);
        latest.EmailSuggestionId.ShouldBeNull();
    }

    [Fact]
    public async Task Status_History_Of_Another_Users_Application_Is_Not_Found()
    {
        var owner = await RegisterAndAuthenticateAsync("history.owner@example.com");
        var created = await CreateApplicationAsync(owner, "Owner Co");

        var stranger = await RegisterAndAuthenticateAsync("history.stranger@example.com");
        var response = await stranger.GetAsync($"/api/applications/{created.Id}/status-history");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
