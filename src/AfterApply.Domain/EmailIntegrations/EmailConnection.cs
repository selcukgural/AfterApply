using AfterApply.Domain.Common;

namespace AfterApply.Domain.EmailIntegrations;

public sealed class EmailConnection : AuditableEntity
{
    public Guid UserId { get; private set; }

    public EmailProvider Provider { get; private set; }

    public string ProviderAccountEmail { get; private set; } = string.Empty;

    /// <summary>The opaque local-part of the user's personal inbound address (e.g. "k7x9m2p4qz" in
    /// "k7x9m2p4qz@application.ekariyerim.com"). Deliberately not the UserId itself, so a leaked
    /// address can't be tied back to the account.</summary>
    public string? InboundToken { get; private set; }

    public DateTimeOffset ConnectedAt { get; private set; }

    /// <summary>Gmail requires confirming a forwarding address via a code/link it emails to that
    /// address before forwarding activates. These three are singleton, 1:1 pending-state fields
    /// (not a history table — a user has at most one outstanding confirmation at a time, and this
    /// connection is already unique per (UserId, Provider)); set by
    /// EmailForwardingService.ProcessInboundEmailAsync when it detects Gmail's own confirmation
    /// email, cleared once the user dismisses it having completed the confirmation in Gmail.</summary>
    public string? GmailConfirmationCode { get; private set; }

    public string? GmailConfirmationLink { get; private set; }

    public DateTimeOffset? GmailConfirmationReceivedAt { get; private set; }

    private EmailConnection()
    {
    }

    /// <summary>providerAccountEmail stores the full forwarding address, e.g.
    /// "connected as: k7x9m2p4qz@application.ekariyerim.com" in Settings.</summary>
    public static EmailConnection CreateForwarding(Guid userId, string inboundToken, string forwardingAddress, DateTimeOffset now)
    {
        return new EmailConnection
        {
            UserId = userId,
            Provider = EmailProvider.Forwarding,
            ProviderAccountEmail = forwardingAddress,
            InboundToken = inboundToken,
            ConnectedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Overwrites any prior pending confirmation — Gmail resends a fresh code/link if the
    /// user retries adding the forwarding address, and only the latest one is ever actionable.</summary>
    public void SetGmailConfirmation(string? code, string? link, DateTimeOffset receivedAt)
    {
        GmailConfirmationCode = code;
        GmailConfirmationLink = link;
        GmailConfirmationReceivedAt = receivedAt;
    }

    public void ClearGmailConfirmation()
    {
        GmailConfirmationCode = null;
        GmailConfirmationLink = null;
        GmailConfirmationReceivedAt = null;
    }
}
