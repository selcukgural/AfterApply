using AfterApply.Domain.Common;

namespace AfterApply.Domain.EmailIntegrations;

public sealed class EmailConnection : AuditableEntity
{
    public Guid UserId { get; private set; }

    public EmailProvider Provider { get; private set; }

    public string ProviderAccountEmail { get; private set; } = string.Empty;

    public string? EncryptedRefreshToken { get; private set; }

    public string GrantedScopes { get; private set; } = string.Empty;

    /// <summary>Only set for Provider == Forwarding — the opaque local-part of the user's personal
    /// inbound address (e.g. "k7x9m2p4qz" in "k7x9m2p4qz@application.ekariyerim.com"). Deliberately
    /// not the UserId itself, so a leaked address can't be tied back to the account.</summary>
    public string? InboundToken { get; private set; }

    public DateTimeOffset ConnectedAt { get; private set; }

    public DateTimeOffset? DisconnectedAt { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public string? LastSyncError { get; private set; }

    public DateTimeOffset? LastSyncErrorAt { get; private set; }

    private EmailConnection()
    {
    }

    public static EmailConnection Create(Guid userId, EmailProvider provider, string providerAccountEmail,
        string encryptedRefreshToken, string grantedScopes, DateTimeOffset now)
    {
        return new EmailConnection
        {
            UserId = userId,
            Provider = provider,
            ProviderAccountEmail = providerAccountEmail,
            EncryptedRefreshToken = encryptedRefreshToken,
            GrantedScopes = grantedScopes,
            ConnectedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Creates a Forwarding-provider connection: no OAuth token ever (EncryptedRefreshToken
    /// stays null forever for this provider), identified instead by an opaque inbound-address token.
    /// providerAccountEmail stores the full forwarding address for display symmetry with the Gmail
    /// card (e.g. Settings shows "connected as: k7x9m2p4qz@application.ekariyerim.com").</summary>
    public static EmailConnection CreateForwarding(Guid userId, string inboundToken, string forwardingAddress, DateTimeOffset now)
    {
        return new EmailConnection
        {
            UserId = userId,
            Provider = EmailProvider.Forwarding,
            ProviderAccountEmail = forwardingAddress,
            InboundToken = inboundToken,
            GrantedScopes = string.Empty,
            ConnectedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Re-establishes a previously disconnected connection in place (same row, same
    /// unique (UserId, Provider) identity) rather than creating a new one.</summary>
    public void Reconnect(string encryptedRefreshToken, string providerAccountEmail, string grantedScopes, DateTimeOffset now)
    {
        EncryptedRefreshToken = encryptedRefreshToken;
        ProviderAccountEmail = providerAccountEmail;
        GrantedScopes = grantedScopes;
        ConnectedAt = now;
        DisconnectedAt = null;
        LastSyncError = null;
        LastSyncErrorAt = null;
        Touch(now);
    }

    /// <summary>Stops future syncing and clears the refresh token, but does not delete this row —
    /// existing EmailSuggestions tied to it are intentionally preserved (product decision).</summary>
    public void Disconnect(DateTimeOffset now)
    {
        DisconnectedAt = now;
        EncryptedRefreshToken = null;
        Touch(now);
    }

    public void UpdateAfterSync(DateTimeOffset syncedAt)
    {
        LastSyncedAt = syncedAt;
        LastSyncError = null;
        LastSyncErrorAt = null;
        Touch(syncedAt);
    }

    public void RecordSyncFailure(string error, DateTimeOffset now)
    {
        LastSyncError = error;
        LastSyncErrorAt = now;
        Touch(now);
    }
}
