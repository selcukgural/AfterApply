namespace AfterApply.Application.EmailIntegrations;

/// <summary>Port to the real Gmail API. Lets sync/classification/matching logic be tested with a
/// fake implementation instead of hitting live Gmail — the real implementation (GmailClient,
/// Infrastructure layer) is exercised manually once real OAuth credentials exist.</summary>
public interface IGmailClient
{
    Task<GmailProfile> GetProfileAsync(UserCredentialToken token, CancellationToken cancellationToken);

    Task<IReadOnlyList<GmailMessageSummary>> ListMessagesSinceAsync(UserCredentialToken token, DateTimeOffset since, CancellationToken cancellationToken);

    Task<GmailMessageDetail?> GetMessageDetailAsync(UserCredentialToken token, string messageId, CancellationToken cancellationToken);

    Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
