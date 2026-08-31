using AfterApply.Application.EmailIntegrations.Contracts;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>Fed by a Cloudflare Email Worker relaying mail the user forwards themselves via their
/// own mail provider's filter — the only ingestion path this app has (the earlier Gmail-OAuth path
/// was removed, see project memory / DECISIONS.md).</summary>
public interface IEmailForwardingService
{
    /// <summary>Returns the user's personal forwarding address, creating their Forwarding
    /// EmailConnection (and its opaque token) on first call.</summary>
    Task<string> GetOrCreateInboundAddressAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Processes one forwarded email. No-ops (logs, doesn't throw) when the address's token
    /// isn't recognized — an unknown/stale address is not the caller's fault to react to.</summary>
    Task ProcessInboundEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    Task<bool> DismissSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);
}

public sealed record InboundEmailRequest(
    string ToAddress, string FromEmail, string FromDisplayName, string Subject, string Snippet,
    DateTimeOffset ReceivedAt);
