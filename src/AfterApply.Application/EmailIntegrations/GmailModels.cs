namespace AfterApply.Application.EmailIntegrations;

public sealed record UserCredentialToken(string RefreshToken);

public sealed record GmailProfile(string EmailAddress);

public sealed record GmailMessageSummary(
    string MessageId,
    string? ThreadId,
    string SenderEmail,
    string SenderDisplayName,
    string Subject,
    string Snippet,
    DateTimeOffset ReceivedAt);

public sealed record GmailMessageDetail(string MessageId, string Subject, string Snippet);

public sealed record GoogleTokenResponse(string RefreshToken, string AccessToken, DateTimeOffset AccessTokenExpiresAt);
