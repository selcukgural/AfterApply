using AfterApply.Domain.Common;

namespace AfterApply.Domain.Feedback;

/// <summary>
/// One message a user sent from the in-app feedback panel.
///
/// Named FeedbackEntry rather than Feedback so the type does not shadow its own namespace — the
/// same reason Domain.Applications.Application has to be aliased everywhere it is used.
///
/// This row is the canonical record. When the GitHub mirror is enabled a background job copies a
/// redacted version of it into an issue (see GitHubIssueMirror) and writes the issue number back
/// here; the mirror is a convenience for triage, never the source of truth, so turning it off or
/// losing the repository costs nothing but the issue list.
/// </summary>
public sealed class FeedbackEntry : AuditableEntity
{
    public Guid UserId { get; private set; }

    public FeedbackCategory Category { get; private set; }

    public FeedbackMood? Mood { get; private set; }

    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Where to answer, when the user chose to be answered. Deliberately separate from the account
    /// email: giving feedback and wanting a reply are different decisions, and this one is opt-in.
    /// Never leaves the database — the GitHub mirror redacts it (see GitHubIssueMirror).
    /// </summary>
    public string? ReplyEmail { get; private set; }

    /// <summary>The in-app path the panel was opened from (e.g. <c>/tr/applications</c>). The whole
    /// point of the floating panel: it knows which screen the user was stuck on so they don't have
    /// to describe it. Path only — never the query string, which can carry ids.</summary>
    public string? PagePath { get; private set; }

    public string? Locale { get; private set; }

    public string? Theme { get; private set; }

    /// <summary>Read from the request header, not the request body: the client has no reason to be
    /// the one asserting this, and the header is already there.</summary>
    public string? UserAgent { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// Set by hand (SQL) for now — there is no admin UI yet, and adding a mutator nothing calls
    /// would be dead code. Modelled today so the rows collected from day one carry the history the
    /// planned "your feedback" screen will read.
    /// </summary>
    public FeedbackStatus Status { get; private set; }

    public string? AdminReply { get; private set; }

    public DateTimeOffset? AdminReplyAt { get; private set; }

    public int? GitHubIssueNumber { get; private set; }

    public string? GitHubIssueUrl { get; private set; }

    public DateTimeOffset? MirroredAt { get; private set; }

    private FeedbackEntry()
    {
    }

    public static FeedbackEntry Create(Guid userId, FeedbackCategory category, FeedbackMood? mood,
        string message, string? replyEmail, string? pagePath, string? locale, string? theme,
        string? userAgent, DateTimeOffset now)
    {
        return new FeedbackEntry
        {
            UserId = userId,
            Category = category,
            Mood = mood,
            Message = message,
            ReplyEmail = replyEmail,
            PagePath = pagePath,
            Locale = locale,
            Theme = theme,
            UserAgent = userAgent,
            SubmittedAt = now,
            Status = FeedbackStatus.Received,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Records where the mirrored issue landed, so a row is never mirrored twice and so
    /// the issue can be found from the database side.</summary>
    public void RecordMirroredIssue(int issueNumber, string issueUrl, DateTimeOffset now)
    {
        GitHubIssueNumber = issueNumber;
        GitHubIssueUrl = issueUrl;
        MirroredAt = now;
        Touch(now);
    }
}
