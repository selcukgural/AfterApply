namespace AfterApply.Infrastructure.EmailIntegrations;

public sealed class GoogleOAuthOptions
{
    public string ClientId { get; init; } = "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID";

    public string ClientSecret { get; init; } = "REPLACE_WITH_GOOGLE_OAUTH_CLIENT_SECRET";

    public string RedirectUri { get; init; } = "http://localhost:5151/api/email-integrations/gmail/callback";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !ClientId.StartsWith("REPLACE_WITH_", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(ClientSecret) && !ClientSecret.StartsWith("REPLACE_WITH_", StringComparison.Ordinal);
}
