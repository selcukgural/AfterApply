namespace AfterApply.Domain.Feedback;

/// <summary>
/// Where a piece of feedback stands. Everything lands on <see cref="Received"/>; the later values
/// exist because the "what happened to my feedback?" screen is a decided, not-yet-built follow-up
/// (2026-09-07) and backfilling a status onto rows collected without one loses the history.
/// </summary>
public enum FeedbackStatus
{
    Received = 0,
    UnderReview = 1,
    Planned = 2,
    Resolved = 3,
    Declined = 4
}
