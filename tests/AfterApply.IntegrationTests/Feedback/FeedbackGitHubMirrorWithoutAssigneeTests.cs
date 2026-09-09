using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Feedback;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Feedback;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Feedback;

/// <summary>
/// The mirror with no assignee configured — which is the default, and what anyone setting this up
/// from .env.prod.example gets. It needs its own host because the assignee is read from
/// configuration at startup, and it is worth the extra host: GitHub rejects a null "assignees",
/// so getting the omission wrong would break every mirrored issue for the default configuration
/// while <see cref="FeedbackGitHubMirrorTests"/>, which sets one, stayed green.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FeedbackGitHubMirrorWithoutAssigneeTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private StubGitHubHandler _handler = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(FeedbackGitHubMirrorWithoutAssigneeTests));
        _handler = new StubGitHubHandler();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // This class asserts what a background job did, so it needs the one thing the suite
            // switches off by default — see TestContainerCleanup.DisableHangfireServerForTests.
            builder.UseSetting("Hangfire:ServerEnabled", "true");
            // Turning the mirror back on for this class only. TestContainerCleanup forces it off
            // process-wide through environment variables, which sit above user secrets — the
            // whole point being that a developer's live token can never reach a test host. An
            // in-memory source added here is appended last, so it beats those variables.
            // Whitespace rather than absent for the assignee: a half-filled value has to behave
            // the same as an empty one, or a stray space breaks the mirror for whoever leaves it.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:GitHub:Enabled"] = "true",
                ["Feedback:GitHub:Repository"] = "owner/repo",
                ["Feedback:GitHub:Token"] = "test-token",
                ["Feedback:GitHub:Assignee"] = "   ",
            }));

            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(IGitHubIssueMirror))
                    .ConfigurePrimaryHttpMessageHandler(() => _handler));
        });

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("feedback.noassignee@example.com", "P@ssw0rd123!", "Mirror", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await TestHostDisposal.DisposeQuietlyAsync(_factory);
        }
    }

    [Fact]
    public async Task With_No_Assignee_Configured_The_Field_Is_Omitted_Entirely()
    {
        var response = await _client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Question, "Where do I find my CVs?"), JsonOptions);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        var stored = await PollUntilMirroredAsync(receipt!.Id);
        stored.GitHubIssueNumber.ShouldBe(412);

        // Not "assignees": null and not "assignees": [] — the key must not be in the payload at all.
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Body.ShouldNotContain("assignees");
    }

    private async Task<FeedbackEntry> PollUntilMirroredAsync(Guid feedbackEntryId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            using (var scope = _factory!.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var entry = await dbContext.FeedbackEntries.AsNoTracking()
                    .SingleAsync(f => f.Id == feedbackEntryId);
                if (entry.MirroredAt is not null)
                {
                    return entry;
                }
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Feedback {feedbackEntryId} was not mirrored within 60s.");
    }
}
