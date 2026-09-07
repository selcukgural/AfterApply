namespace AfterApply.Domain.Applications;

/// <summary>How a status change was actually applied — the "who did this" the user sees on the
/// application's status history. Deliberately finer-grained than <see cref="Common.Source"/>: an
/// email-driven change is Source.Email whether the user confirmed the suggestion or the classifier
/// applied it unattended, and that is exactly the distinction a user looking back wants.
/// Never taken from a client request — the API endpoint always records Manual.</summary>
public enum StatusChangeOrigin
{
    /// <summary>The user changed it from the application detail screen.</summary>
    Manual,

    /// <summary>The user confirmed an email suggestion (EmailForwardingService.ConfirmSuggestionAsync).</summary>
    EmailSuggestionConfirmed,

    /// <summary>Applied unattended because the suggestion cleared the auto-approval bar
    /// (EmailForwardingService.TryAutoApplyAsync).</summary>
    EmailAutoApplied,

    /// <summary>The user undid an unattended auto-apply, putting the status back where it was
    /// (EmailForwardingService.RevertAutoApplyAsync). Its own origin rather than Manual so the
    /// history reads honestly — the user did act, but what they did was correct a mistake the
    /// product made, and that is worth being able to count.</summary>
    EmailAutoApplyReverted,

    /// <summary>Carried in by a CSV or LinkedIn Data Export import.</summary>
    Import,

    /// <summary>Set by the browser extension.</summary>
    Extension,

    /// <summary>Applied by a background rule rather than a person or an email — e.g. ghosting
    /// detection. Not written by any path today; present so adding one later is not a migration.</summary>
    System
}
