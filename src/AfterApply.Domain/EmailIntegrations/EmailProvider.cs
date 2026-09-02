namespace AfterApply.Domain.EmailIntegrations;

public enum EmailProvider
{
    /// <summary>The user forwards status-relevant mail themselves via a filter in their own mail
    /// provider (any provider — Gmail, Outlook, ...) to a personal inbound address; a Cloudflare
    /// Email Worker relays it to the backend. No OAuth token, no broad inbox read access.</summary>
    Forwarding,

    /// <summary>The browser extension's Gmail content script reads an opened thread's
    /// sender/subject/body client-side, scores it locally, and POSTs only the extracted signal for
    /// threads that pass the local filter — the raw email is never forwarded/transmitted. This
    /// connection row carries no InboundToken/real ProviderAccountEmail; it exists only so
    /// EmailSuggestion.EmailConnectionId has something to point at and the existing idempotency
    /// check has something to key off, mirroring Forwarding's role.</summary>
    Extension
}
