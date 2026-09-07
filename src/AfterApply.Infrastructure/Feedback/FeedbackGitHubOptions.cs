namespace AfterApply.Infrastructure.Feedback;

/// <summary>
/// The GitHub Issues mirror, bound from the <c>Feedback:GitHub</c> section. Off unless all three
/// values are set, which is why an unconfigured environment — every local run, and production
/// until the token is in Secret Manager — simply stores feedback and never calls out.
/// </summary>
public sealed class FeedbackGitHubOptions
{
    public const string SectionName = "Feedback:GitHub";

    /// <summary>
    /// <b>This flag and the privacy policy move together, in both directions.</b> The published
    /// page (<c>/{locale}/privacy</c>, sections <c>#feedback</c> and <c>#cross-border-transfer</c>)
    /// names GitHub, Inc. (Microsoft, United States) as a recipient and the mirror as a transfer
    /// abroad. That text was written before this was first switched on (2026-09-07) and has to be
    /// walked back if it is ever switched off — a policy claiming a transfer that no longer happens
    /// is as wrong as one omitting a transfer that does.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>The mirror target as <c>owner/repo</c>. It must be a <b>private</b> repository:
    /// the body carries whatever a user typed into the panel.</summary>
    public string? Repository { get; init; }

    /// <summary>A fine-grained PAT (or GitHub App installation token) with issues:write on that
    /// one repository, and nothing else.</summary>
    public string? Token { get; init; }

    /// <summary>
    /// GitHub username the mirrored issue is assigned to, so a new report lands in that person's
    /// "Assigned to me" rather than waiting to be noticed. Optional — blank leaves issues
    /// unassigned. GitHub silently drops an assignee who cannot be assigned on the repository
    /// (no access, or the name does not exist), so a typo here costs the assignment, not the issue.
    /// </summary>
    public string? Assignee { get; init; }

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(Repository) && !string.IsNullOrWhiteSpace(Token);
}
