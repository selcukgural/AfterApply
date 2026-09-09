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
/// The optional half of the feature: with <c>Feedback:GitHub</c> configured, a background job
/// copies a redacted version of the stored row into an issue. The database row stays canonical —
/// these tests exist mostly to pin what does and does not cross that boundary.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FeedbackGitHubMirrorTests(SharedInfrastructure shared) : IAsyncLifetime
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
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(FeedbackGitHubMirrorTests));
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
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Feedback:GitHub:Enabled"] = "true",
                ["Feedback:GitHub:Repository"] = "owner/repo",
                ["Feedback:GitHub:Token"] = "test-token",
                ["Feedback:GitHub:Assignee"] = "maintainer",
            }));

            // The typed client's name is the interface's short name, so re-registering it here
            // appends to the same named options and replaces the primary handler.
            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(IGitHubIssueMirror))
                    .ConfigurePrimaryHttpMessageHandler(() => _handler));
        });

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("feedback.mirror@example.com", "P@ssw0rd123!", "Mirror", "Test", true), JsonOptions);
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
    public async Task A_Submission_Opens_A_Labelled_Issue_And_Records_Where_It_Landed()
    {
        var response = await _client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "Group my applications by company.",
                FeedbackMood.Good, "private@example.com", "/tr/applications", "tr", "dark"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        var stored = await PollUntilMirroredAsync(receipt!.Id);

        stored.GitHubIssueNumber.ShouldBe(412);
        stored.GitHubIssueUrl.ShouldBe("https://github.com/owner/repo/issues/412");
        stored.MirroredAt.ShouldNotBeNull();

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Uri.ToString().ShouldBe("https://api.github.com/repos/owner/repo/issues");
        request.Authorization.ShouldBe("Bearer test-token");
        request.Body.ShouldContain("feedback:idea");
        request.Body.ShouldContain("Group my applications by company.");
        // Assigned on creation so a new report lands in someone's queue instead of waiting to be
        // spotted.
        request.Body.ShouldContain("\"assignees\":[\"maintainer\"]");
    }

    [Fact]
    public async Task The_Mirror_Carries_The_Message_But_Never_The_Sender()
    {
        var response = await _client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Bug, "The dark theme chart labels are unreadable.",
                ReplyEmail: "private@example.com"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        var stored = await PollUntilMirroredAsync(receipt!.Id);
        var request = _handler.Requests.ShouldHaveSingleItem();

        // The reply address and the account it belongs to stay in our database; the issue carries
        // the feedback id, which is the key to look them up with.
        request.Body.ShouldNotContain("private@example.com");
        request.Body.ShouldNotContain("feedback.mirror@example.com");
        request.Body.ShouldContain(stored.Id.ToString());
        // ...but the reader still has to know an answer is expected.
        request.Body.ShouldContain("Wants a reply");
        stored.ReplyEmail.ShouldBe("private@example.com");
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
