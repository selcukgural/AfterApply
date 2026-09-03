namespace AfterApply.Domain.EmailIntegrations;

public enum EmailProvider
{
    /// <summary>The browser extension's Gmail content script reads an opened thread's
    /// sender/subject/body client-side, scores it locally, and POSTs only the extracted signal for
    /// threads that pass the local filter — the raw email is never forwarded/transmitted. This
    /// connection row carries no real ProviderAccountEmail; it exists only so
    /// EmailSuggestion.EmailConnectionId has something to point at and the existing idempotency
    /// check has something to key off.</summary>
    Extension
}
