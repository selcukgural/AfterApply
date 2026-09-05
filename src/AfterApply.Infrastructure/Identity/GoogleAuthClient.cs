using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// The server half of the authorization-code + PKCE flow: exchanges the code the browser brought
/// back for tokens at Google's token endpoint (https://developers.google.com/identity/protocols/oauth2/web-server#exchange-authorization-code)
/// and reads the identity out of the ID token. Only the ID token is used — no access token is kept,
/// no Google API is called, nothing beyond "who is this" is requested (scopes are openid/email/
/// profile, chosen by the web app when it redirects to Google).
/// </summary>
internal sealed class GoogleAuthClient(
    HttpClient httpClient,
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleAuthClient> logger,
    TimeProvider? timeProvider = null) : IGoogleAuthClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly GoogleAuthOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<GoogleIdentity?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "GoogleAuth:ClientId / GoogleAuth:ClientSecret are not configured. For local dev run " +
                "'dotnet user-secrets set GoogleAuth:ClientId \"...\" --project src/AfterApply.Api' (and ClientSecret), " +
                "or set GoogleAuth__ClientId / GoogleAuth__ClientSecret in the environment.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google token endpoint unreachable");
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Google answers 400 with {"error":"invalid_grant"} for a spent/expired code or a
                // redirect_uri/verifier mismatch — the error code is safe to log, the body is small.
                var error = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken);
                logger.LogWarning("Google code exchange rejected: {Status} {Error}", (int)response.StatusCode, error?.Error);
                return null;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (string.IsNullOrEmpty(tokens?.IdToken))
            {
                logger.LogWarning("Google code exchange succeeded but returned no id_token");
                return null;
            }

            var identity = GoogleIdTokenReader.Read(tokens.IdToken, _options.ClientId!, _timeProvider.GetUtcNow());
            if (identity is null)
            {
                logger.LogWarning("Google id_token failed validation (issuer/audience/expiry/required claims)");
            }

            return identity;
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

    private sealed record TokenErrorResponse([property: JsonPropertyName("error")] string? Error);
}
