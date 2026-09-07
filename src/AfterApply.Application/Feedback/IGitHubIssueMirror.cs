namespace AfterApply.Application.Feedback;

/// <summary>
/// Copies a stored feedback entry into a GitHub issue so triage (labels, milestones, the project
/// board) comes for free. Runs as a Hangfire job after the row is already committed: the mirror is
/// a convenience, never a precondition, so a GitHub outage must not fail a user's submission.
/// </summary>
public interface IGitHubIssueMirror
{
    Task MirrorAsync(Guid feedbackEntryId, CancellationToken cancellationToken);
}
