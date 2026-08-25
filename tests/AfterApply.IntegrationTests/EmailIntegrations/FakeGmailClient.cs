using AfterApply.Application.EmailIntegrations;

namespace AfterApply.IntegrationTests.EmailIntegrations;

/// <summary>Test double for IGmailClient, registered into WebApplicationFactory's DI container in
/// place of the real GmailClient — lets OAuth/sync/confirmation flows be integration-tested
/// without real Gmail credentials.</summary>
public sealed class FakeGmailClient : IGmailClient
{
    public GmailProfile Profile { get; set; } = new("test@gmail.com");

    public List<GmailMessageSummary> Messages { get; } = [];

    public Dictionary<string, GmailMessageDetail> MessageDetails { get; } = [];

    public GoogleTokenResponse TokenResponse { get; set; } =
        new("fake-refresh-token", "fake-access-token", DateTimeOffset.UtcNow.AddHours(1));

    public List<string> RevokedTokens { get; } = [];

    public bool ThrowOnRevoke { get; set; }

    public Task<GmailProfile> GetProfileAsync(UserCredentialToken token, CancellationToken cancellationToken) =>
        Task.FromResult(Profile);

    public Task<IReadOnlyList<GmailMessageSummary>> ListMessagesSinceAsync(UserCredentialToken token, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GmailMessageSummary>>(Messages.Where(m => m.ReceivedAt >= since).ToList());

    public Task<GmailMessageDetail?> GetMessageDetailAsync(UserCredentialToken token, string messageId, CancellationToken cancellationToken) =>
        Task.FromResult(MessageDetails.GetValueOrDefault(messageId));

    public Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken) =>
        Task.FromResult(TokenResponse);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (ThrowOnRevoke)
        {
            throw new InvalidOperationException("Simulated revoke failure.");
        }

        RevokedTokens.Add(refreshToken);
        return Task.CompletedTask;
    }
}
