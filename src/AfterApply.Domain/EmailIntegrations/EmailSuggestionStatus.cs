namespace AfterApply.Domain.EmailIntegrations;

public enum EmailSuggestionStatus
{
    Pending,
    Confirmed,
    Dismissed,

    /// <summary>Applied without asking, because the suggestion cleared the auto-approval bar.</summary>
    AutoApplied,

    /// <summary>Auto-applied, and then undone by the user.
    ///
    /// Deliberately its own value rather than folding back into Dismissed. Dismissed means the user
    /// was <i>shown</i> a suggestion and declined it — a low-friction "no" to a question they were
    /// asked. Reverted means the product acted unattended and the user had to take it back, which is
    /// the only signal that actually answers "would auto-applying this have been right?". Collapsing
    /// the two would destroy the one unbiased measurement the feature produces; see
    /// EmailAutoApprovalOptions and the calibration endpoint.</summary>
    Reverted
}
