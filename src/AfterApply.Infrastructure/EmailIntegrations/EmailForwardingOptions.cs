namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Config for the Cloudflare inbound-forwarding pipeline.</summary>
public sealed class EmailForwardingOptions
{
    public bool Enabled { get; init; } = false;

    /// <summary>The subdomain users forward mail to, e.g. "application.ekariyerim.com". Combined
    /// with a per-user opaque token to form the full personal address.</summary>
    public string Domain { get; init; } = "application.ekariyerim.com";

    /// <summary>Shared secret the Cloudflare Worker sends in the X-Webhook-Secret header. Null/empty
    /// means the inbound endpoint is not configured — same "inert until set" pattern as
    /// OpenAiOptions.</summary>
    public string? WebhookSecret { get; init; }
}
