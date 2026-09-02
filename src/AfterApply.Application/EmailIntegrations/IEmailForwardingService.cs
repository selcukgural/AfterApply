using AfterApply.Application.EmailIntegrations.Contracts;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>Fed by a Cloudflare Email Worker relaying mail the user forwards themselves via their
/// own mail provider's filter — the only ingestion path this app has (the earlier Gmail-OAuth path
/// was removed, see project memory / DECISIONS.md).</summary>
public interface IEmailForwardingService
{
    /// <summary>Returns the user's personal forwarding address (creating their Forwarding
    /// EmailConnection and its opaque token on first call), plus any pending Gmail
    /// forwarding-confirmation code/link waiting to be acknowledged.</summary>
    Task<InboundAddressResponse> GetOrCreateInboundAddressAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Processes one forwarded email. No-ops (logs, doesn't throw) when the address's token
    /// isn't recognized — an unknown/stale address is not the caller's fault to react to. An email
    /// recognized as Gmail's own forwarding-confirmation message is stored on the EmailConnection
    /// (see EmailConnection.SetGmailConfirmation) instead of going through suggestion
    /// classification.</summary>
    Task ProcessInboundEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken);

    /// <summary>Processes one signal the Gmail content script extracted client-side from a thread
    /// the user opened and scored as plausibly job-related — never the raw email, never anything
    /// about mail the user didn't open. Lazily creates the user's Extension-provider EmailConnection
    /// on first call, then shares the same classify/match/auto-apply/persist pipeline
    /// ProcessInboundEmailAsync uses.</summary>
    Task ProcessExtensionSignalAsync(Guid userId, ExtensionEmailSignalRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Cheap count-only variant of <see cref="GetPendingSuggestionsAsync"/> for UI badges —
    /// skips the Applications/Companies joins, backed by the same (UserId, Status) index.</summary>
    Task<int> GetPendingSuggestionCountAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    Task<bool> DismissSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    /// <summary>Clears a pending Gmail confirmation once the user has completed it in Gmail's own
    /// UI. Returns false when there was nothing pending.</summary>
    Task<bool> DismissGmailConfirmationAsync(Guid userId, CancellationToken cancellationToken);

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

/// <summary>LinkDomains are the deduped lowercased hostnames of any links in the email's HTML body
/// (e.g. "greenhouse.io", "calendly.com") — never full URLs, since a query string can carry
/// tracking/PII. Extracted by the Cloudflare Worker (email-worker/src/index.js), fed into
/// RecruitmentSignalAnalyzer.</summary>
public sealed record InboundEmailRequest(
    string ToAddress, string FromEmail, string FromDisplayName, string Subject, string Snippet,
    DateTimeOffset ReceivedAt, IReadOnlyList<string> LinkDomains);

/// <summary>Shape the Gmail content script POSTs — sender/subject/snippet it read directly from the
/// opened thread's DOM (subject/body capped client-side before this is ever built), never the raw
/// email. GmailMessageId is Gmail's own id for the thread (from location.hash), the idempotency
/// key's raw material — no ToAddress, since auth already resolves the user.</summary>
public sealed record ExtensionEmailSignalRequest(
    string SenderEmail, string SenderDisplayName, string Subject, string Snippet,
    DateTimeOffset ReceivedAt, IReadOnlyList<string> LinkDomains, string GmailMessageId);
