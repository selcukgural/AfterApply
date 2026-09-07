namespace AfterApply.Domain.Feedback;

/// <summary>
/// The optional "how is it going?" opener on the panel. Carries no triage meaning on its own; it
/// exists so a trend is measurable later without going back and asking everyone again.
/// </summary>
public enum FeedbackMood
{
    Struggling = 0,
    Okay = 1,
    Good = 2
}
