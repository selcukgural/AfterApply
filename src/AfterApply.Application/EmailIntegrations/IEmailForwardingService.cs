using AfterApply.Application.EmailIntegrations.Contracts;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>Fed by the browser extension's Gmail content script — the only ingestion path this app
/// has (the earlier forward-all-inbox-to-us design, and before that the Gmail-OAuth path, were both
/// removed; see project memory / DECISIONS.md).</summary>
public interface IEmailForwardingService
{
    /// <summary>Processes one signal the Gmail content script extracted client-side from a thread
    /// the user opened and scored as plausibly job-related — never the raw email, never anything
    /// about mail the user didn't open. Lazily creates the user's Extension-provider EmailConnection
    /// on first call, then runs the shared classify/match/auto-apply/persist pipeline.</summary>
    Task ProcessExtensionSignalAsync(Guid userId, ExtensionEmailSignalRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Cheap count-only variant of <see cref="GetPendingSuggestionsAsync"/> for UI badges —
    /// skips the Applications/Companies joins, backed by the same (UserId, Status) index.</summary>
    Task<int> GetPendingSuggestionCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    Task<bool> DismissSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    /// <summary>AutoApplied and Confirmed suggestions, newest first — the Notifications screen's
    /// event log.</summary>
    Task<IReadOnlyList<EmailNotificationResponse>> GetNotificationsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Cheap count-only variant for the nav badge — only AutoApplied &amp; unread counts,
    /// since a Confirmed suggestion is something the user already knowingly did themselves.</summary>
    Task<int> GetUnreadNotificationCountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks every currently-unread notification for the user as read — fired once when the
    /// notifications page loads, not per-row.</summary>
    Task MarkNotificationsReadAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Shape the Gmail content script POSTs — sender/subject/snippet it read directly from the
/// opened thread's DOM (subject/body capped client-side before this is ever built), never the raw
/// email. GmailMessageId is Gmail's own id for the thread (from location.hash), the idempotency
/// key's raw material — no ToAddress, since auth already resolves the user.
///
/// LinkDomains is declared nullable because it genuinely is: nullable reference types are a
/// compile-time contract with no runtime enforcement, and System.Text.Json writes null into this
/// property for both <c>"linkDomains": null</c> and an omitted field (measured, not assumed). While
/// it was declared non-nullable the declaration was simply false, and any null guard written
/// against it read as redundant to the analyzer — which is how a validator predicate that
/// dereferenced it got written, turning a malformed body into a 500. The other properties are
/// left non-nullable and are covered by NotEmpty rules in the validator instead: for a string,
/// null and "" fail the same rule, so nothing is lost by not spelling out the nullability.</summary>
public sealed record ExtensionEmailSignalRequest(
    string SenderEmail, string SenderDisplayName, string Subject, string Snippet,
    DateTimeOffset ReceivedAt, IReadOnlyList<string>? LinkDomains, string GmailMessageId);
