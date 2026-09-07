using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AfterApply.Application.Feedback;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Feedback;

/// <summary>
/// Opens the mirrored GitHub issue for one stored feedback entry. See GitHubIssueComposer for what
/// does and does not cross the boundary — the short version is that the message and its technical
/// context go, and everything identifying the sender stays here.
/// </summary>
internal sealed partial class GitHubIssueMirror(
    HttpClient httpClient,
    AppDbContext dbContext,
    IOptions<FeedbackGitHubOptions> options,
    ILogger<GitHubIssueMirror> logger) : IGitHubIssueMirror
{
    public async Task MirrorAsync(Guid feedbackEntryId, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Re-checked here, not just at the enqueue site: a job can outlive the configuration that
        // queued it (a redeploy that turns the mirror off while the queue still has work).
        if (!settings.IsConfigured)
        {
            return;
        }

        // Built into a URL, so it is validated even though it is our own config — a value like
        // "../../user/repos" would otherwise point this POST at a different API route entirely.
        if (!RepositorySlug().IsMatch(settings.Repository!))
        {
            logger.LogError("Feedback:GitHub:Repository is not a valid owner/repo slug; mirror skipped.");
            return;
        }

        var entry = await dbContext.FeedbackEntries
            .FirstOrDefaultAsync(f => f.Id == feedbackEntryId, cancellationToken);
        if (entry is null)
        {
            return;
        }

        // Hangfire retries, and a retry that reaches this far has already created the issue —
        // without this a transient failure after a successful POST would open duplicates.
        if (entry.GitHubIssueNumber is not null)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/{settings.Repository}/issues")
        {
            Content = JsonContent.Create(new GitHubIssueRequest(
                GitHubIssueComposer.Title(entry),
                GitHubIssueComposer.Body(entry),
                [GitHubIssueComposer.Label(entry.Category)],
                string.IsNullOrWhiteSpace(settings.Assignee) ? null : [settings.Assignee.Trim()]))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Status only. The response body can echo the request back, and the request carries the
            // user's message — logs are not the place for it.
            logger.LogWarning("GitHub rejected the feedback mirror for {FeedbackEntryId} with {StatusCode}.",
                feedbackEntryId, (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var created = await response.Content.ReadFromJsonAsync<GitHubIssueResponse>(cancellationToken);
        if (created is null or { Number: 0 })
        {
            logger.LogWarning("GitHub accepted the feedback mirror for {FeedbackEntryId} but returned no issue number.",
                feedbackEntryId);
            return;
        }

        entry.RecordMirroredIssue(created.Number, created.HtmlUrl ?? string.Empty, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositorySlug();

    /// <summary>Assignees is omitted rather than sent as null when nobody is configured — the
    /// GitHub API rejects a null there, and "no assignee" is the absence of the field.</summary>
    private sealed record GitHubIssueRequest(
        string Title,
        string Body,
        string[] Labels,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Assignees);

    private sealed record GitHubIssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
