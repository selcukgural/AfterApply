namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Kill switch for the whole /api/email-forwarding route group (Gmail Scanning's
/// extension-signal intake plus the shared suggestions/notifications endpoints). Named
/// EmailForwarding/"EmailForwarding" config section for compatibility with already-deployed
/// config — the earlier forward-all-inbox-to-us design this originally gated was removed
/// entirely, see DECISIONS.md.</summary>
public sealed class EmailForwardingOptions
{
    public bool Enabled { get; init; } = false;
}
