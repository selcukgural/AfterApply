namespace AfterApply.Domain.Feedback;

/// <summary>
/// What the user says their message is about. Drives triage — it is the GitHub label the mirrored
/// issue gets — so it is required, unlike <see cref="FeedbackMood"/>.
/// </summary>
public enum FeedbackCategory
{
    Bug = 0,
    Idea = 1,
    Question = 2
}
