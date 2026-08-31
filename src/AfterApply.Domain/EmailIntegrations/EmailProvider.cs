namespace AfterApply.Domain.EmailIntegrations;

public enum EmailProvider
{
    Gmail,

    /// <summary>The user forwards status-relevant mail themselves via a filter in their own mail
    /// provider (any provider — Gmail, Outlook, ...) to a personal inbound address; a Cloudflare
    /// Email Worker relays it to the backend. No OAuth token, no broad inbox read access.</summary>
    Forwarding
}
