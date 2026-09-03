using AfterApply.Domain.Common;

namespace AfterApply.Domain.EmailIntegrations;

public sealed class EmailConnection : AuditableEntity
{
    public Guid UserId { get; private set; }

    public EmailProvider Provider { get; private set; }

    public string ProviderAccountEmail { get; private set; } = string.Empty;

    public DateTimeOffset ConnectedAt { get; private set; }

    private EmailConnection()
    {
    }

    /// <summary>Lazily created on the first client-side-extracted signal a user's Gmail content
    /// script submits — see EmailProvider.Extension. No real ProviderAccountEmail (there's no
    /// single "connected account" concept here, just a row for EmailSuggestion.EmailConnectionId and
    /// the idempotency check to key off).</summary>
    public static EmailConnection CreateExtension(Guid userId, DateTimeOffset now)
    {
        return new EmailConnection
        {
            UserId = userId,
            Provider = EmailProvider.Extension,
            ConnectedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
