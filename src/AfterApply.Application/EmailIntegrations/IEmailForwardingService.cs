namespace AfterApply.Application.EmailIntegrations;

/// <summary>Sibling ingestion path to IEmailIntegrationService's Gmail-OAuth sync — this one is fed
/// by a Cloudflare Email Worker relaying mail the user forwards themselves via their own mail
/// provider's filter, not by polling an OAuth-connected inbox. Produces the same EmailSuggestion
/// rows via the same EmailApplicationMatcher/RuleBasedEmailClassifier/IEmailClassificationProvider
/// pipeline EmailIntegrationService already uses.</summary>
public interface IEmailForwardingService
{
    /// <summary>Returns the user's personal forwarding address, creating their Forwarding
    /// EmailConnection (and its opaque token) on first call.</summary>
    Task<string> GetOrCreateInboundAddressAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Processes one forwarded email. No-ops (logs, doesn't throw) when the address's token
    /// isn't recognized — an unknown/stale address is not the caller's fault to react to.</summary>
    Task ProcessInboundEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken);
}

public sealed record InboundEmailRequest(
    string ToAddress, string FromEmail, string FromDisplayName, string Subject, string Snippet,
    DateTimeOffset ReceivedAt);
