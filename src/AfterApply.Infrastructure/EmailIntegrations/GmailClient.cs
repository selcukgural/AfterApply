using AfterApply.Application.EmailIntegrations;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Real Gmail API implementation of IGmailClient. Not exercised by automated tests (no
/// real OAuth credentials in CI/dev by default) — DI wiring is in place and ready for a manual
/// smoke test once the user supplies real Google Cloud Console credentials (see README.md
/// "Gmail Integration Setup"). Deliberately does not use Google.Apis.Auth.AspNetCore3 — that
/// package assumes MVC controllers + cookie/session state, which doesn't fit this stateless
/// JWT-API + SPA shape; building the authorization URL and doing the code exchange directly via
/// GoogleAuthorizationCodeFlow (used in EmailIntegrationService/here) is simpler and self-contained.</summary>
internal sealed class GmailClient(IOptions<GoogleOAuthOptions> options) : IGmailClient
{
    private const string ApplicationName = "AfterApply";

    public async Task<GmailProfile> GetProfileAsync(UserCredentialToken token, CancellationToken cancellationToken)
    {
        using var service = CreateService(token);
        var profile = await service.Users.GetProfile("me").ExecuteAsync(cancellationToken);
        return new GmailProfile(profile.EmailAddress);
    }

    public async Task<IReadOnlyList<GmailMessageSummary>> ListMessagesSinceAsync(UserCredentialToken token, DateTimeOffset since, CancellationToken cancellationToken)
    {
        using var service = CreateService(token);

        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = $"after:{since.ToUnixTimeSeconds()}";

        var listResponse = await listRequest.ExecuteAsync(cancellationToken);
        if (listResponse.Messages is null || listResponse.Messages.Count == 0)
        {
            return [];
        }

        var summaries = new List<GmailMessageSummary>();

        foreach (var messageRef in listResponse.Messages)
        {
            var detail = await GetMetadataAsync(service, messageRef.Id, cancellationToken);
            if (detail is null)
            {
                continue;
            }

            var (message, subject, from, to) = detail.Value;
            var (senderEmail, senderDisplayName) = ParseFromHeader(from);
            var (recipientEmail, _) = ParseFromHeader(to);

            summaries.Add(new GmailMessageSummary(
                message.Id, message.ThreadId, senderEmail, senderDisplayName, recipientEmail, subject, message.Snippet ?? "",
                message.InternalDate is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : DateTimeOffset.UtcNow));
        }

        return summaries;
    }

    public async Task<GmailMessageDetail?> GetMessageDetailAsync(UserCredentialToken token, string messageId, CancellationToken cancellationToken)
    {
        using var service = CreateService(token);

        var detail = await GetMetadataAsync(service, messageId, cancellationToken);
        return detail is null ? null : new GmailMessageDetail(detail.Value.Message.Id, detail.Value.Subject, detail.Value.Message.Snippet ?? "");
    }

    private static async Task<(Google.Apis.Gmail.v1.Data.Message Message, string Subject, string From, string To)?> GetMetadataAsync(
        GmailService service, string messageId, CancellationToken cancellationToken)
    {
        var getRequest = service.Users.Messages.Get("me", messageId);
        getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
        getRequest.MetadataHeaders = new Repeatable<string>(["Subject", "From", "To"]);

        Google.Apis.Gmail.v1.Data.Message message;
        try
        {
            message = await getRequest.ExecuteAsync(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }

        var headers = message.Payload?.Headers ?? [];
        var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "";
        var from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "";
        var to = headers.FirstOrDefault(h => h.Name == "To")?.Value ?? "";

        return (message, subject, from, to);
    }

    public async Task<GoogleTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var flow = CreateFlow();
        var tokenResponse = await flow.ExchangeCodeForTokenAsync("user", code, redirectUri, cancellationToken);

        return new GoogleTokenResponse(
            tokenResponse.RefreshToken,
            tokenResponse.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(
            $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(refreshToken)}",
            content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private GoogleAuthorizationCodeFlow CreateFlow()
    {
        var opts = options.Value;
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = opts.ClientId, ClientSecret = opts.ClientSecret },
            Scopes = [GmailService.Scope.GmailReadonly]
        });
    }

    private GmailService CreateService(UserCredentialToken token)
    {
        var flow = CreateFlow();
        var credential = new UserCredential(flow, "user", new TokenResponse { RefreshToken = token.RefreshToken });

        return new GmailService(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
            HttpClientInitializer = credential
        });
    }

    private static (string Email, string DisplayName) ParseFromHeader(string from)
    {
        // "Jane Doe <jane@acme.com>" or just "jane@acme.com"
        var start = from.IndexOf('<');
        var end = from.IndexOf('>');
        if (start >= 0 && end > start)
        {
            var email = from[(start + 1)..end].Trim();
            var displayName = from[..start].Trim().Trim('"');
            return (email, displayName.Length > 0 ? displayName : email);
        }

        return (from.Trim(), from.Trim());
    }
}
