using AfterApply.Application.Feedback;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Domain.Feedback;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Feedback;

internal sealed class FeedbackService(
    AppDbContext dbContext,
    IBackgroundJobClient jobClient,
    IOptions<FeedbackGitHubOptions> gitHubOptions) : IFeedbackService
{
    /// <summary>Long enough for any real browser's string; the column is bounded so a crafted
    /// header cannot be used to write a kilobyte per request.</summary>
    private const int MaxUserAgentLength = 400;

    public async Task<FeedbackResponse> SubmitAsync(Guid userId, SubmitFeedbackRequest request,
        string? userAgent, CancellationToken cancellationToken)
    {
        var entry = FeedbackEntry.Create(
            userId,
            request.Category,
            request.Mood,
            request.Message.Trim(),
            NullIfBlank(request.ReplyEmail),
            NormalizePagePath(request.PagePath),
            NullIfBlank(request.Locale),
            NullIfBlank(request.Theme),
            Truncate(NullIfBlank(userAgent), MaxUserAgentLength),
            DateTimeOffset.UtcNow);

        dbContext.FeedbackEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        // After the commit, and enqueued rather than awaited: the user's message is already safe,
        // so a slow or unreachable GitHub must not show up as a failed submission. The job re-checks
        // the flag itself — this one only avoids queueing work that would immediately no-op.
        if (gitHubOptions.Value.IsConfigured)
        {
            jobClient.Enqueue<IGitHubIssueMirror>(m => m.MirrorAsync(entry.Id, CancellationToken.None));
        }

        return new FeedbackResponse(entry.Id, entry.SubmittedAt);
    }

    /// <summary>The validator already rejects anything that is not a path, but a query string is
    /// stripped rather than rejected: an older client sending <c>/tr/applications?status=Applied</c>
    /// should still get its feedback through, just without the parameters — those can carry ids.</summary>
    private static string? NormalizePagePath(string? pagePath)
    {
        var path = NullIfBlank(pagePath);
        if (path is null)
        {
            return null;
        }

        var cut = path.IndexOfAny(['?', '#']);
        return cut < 0 ? path : NullIfBlank(path[..cut]);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
