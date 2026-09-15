using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Feedback;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Feedback;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Feedback;

/// <summary>As <see cref="FeedbackGitHubMirrorProfile" />, with whitespace rather than absent
/// for the assignee: a half-filled value has to behave the same as an empty one, or a stray
/// space breaks the mirror for whoever leaves it.</summary>
public sealed class FeedbackGitHubMirrorWithoutAssigneeProfile : IHostProfile
{
    public StubGitHubHandler GitHub { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Feedback:GitHub:Enabled"] = "true",
            ["Feedback:GitHub:Repository"] = "owner/repo",
            ["Feedback:GitHub:Token"] = "test-token",
            ["Feedback:GitHub:Assignee"] = "   ",
        }));

        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(IGitHubIssueMirror))
                .ConfigurePrimaryHttpMessageHandler(() => GitHub));
    }

    public void Reset() => GitHub.Clear();
}

/// <summary>
/// The mirror with no assignee configured — which is the default, and what anyone setting this up
/// from .env.prod.example gets. It needs its own host because the assignee is read from
/// configuration at startup, and it is worth the extra host: GitHub rejects a null "assignees",
/// so getting the omission wrong would break every mirrored issue for the default configuration
/// while <see cref="FeedbackGitHubMirrorTests"/>, which sets one, stayed green.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FeedbackGitHubMirrorWithoutAssigneeTests(ApiHost<FeedbackGitHubMirrorWithoutAssigneeProfile> host) : IClassFixture<ApiHost<FeedbackGitHubMirrorWithoutAssigneeProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;
    private StubGitHubHandler _handler => host.Profile.GitHub;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("feedback.noassignee@example.com", "P@ssw0rd123!", "Mirror", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await dbContext.FeedbackEntries.AsNoTracking().SingleAsync(f => f.Id == feedbackEntryId);
        entry.MirroredAt.ShouldNotBeNull("the mirror job ran but did not mark the entry");
        return entry;
    }
}
