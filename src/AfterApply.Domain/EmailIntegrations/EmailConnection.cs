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
}
