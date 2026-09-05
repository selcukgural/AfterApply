using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// The server half of LinkedIn's OpenID Connect authorization-code flow: exchanges the code the
/// browser brought back for tokens at LinkedIn's token endpoint and reads the identity out of the ID
/// token, with full JWKS signature verification (<see cref="LinkedInIdTokenReader"/>). Only the ID
/// token is used — no access token is kept, no LinkedIn API is called beyond the token exchange
/// itself, nothing beyond "who is this" is requested (scopes are openid/profile/email, chosen by the
/// web app when it redirects to LinkedIn). No PKCE: LinkedIn's authorization/token endpoints don't
/// call for a code_verifier for a confidential (client-secret-holding) client the way Google's do.
/// </summary>
internal sealed class LinkedInAuthClient(
    HttpClient httpClient,
    IOptions<LinkedInAuthOptions> options,
    LinkedInJwksProvider jwksProvider,
    ILogger<LinkedInAuthClient> logger,
    TimeProvider? timeProvider = null) : ILinkedInAuthClient
{
    private const string TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";

    private readonly LinkedInAuthOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<LinkedInIdentity?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "LinkedInAuth:ClientId / LinkedInAuth:ClientSecret are not configured. For local dev run " +
                "'dotnet user-secrets set LinkedInAuth:ClientId \"...\" --project src/AfterApply.Api' (and ClientSecret), " +
                "or set LinkedInAuth__ClientId / LinkedInAuth__ClientSecret in the environment.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
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
            logger.LogWarning(ex, "LinkedIn token endpoint unreachable");
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // LinkedIn answers 400 with {"error":"invalid_request", ...} for a spent/expired
                // code or a redirect_uri mismatch — the error code is safe to log, the body is small.
                var error = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken);
                logger.LogWarning("LinkedIn code exchange rejected: {Status} {Error}", (int)response.StatusCode, error?.Error);
                return null;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (!string.IsNullOrEmpty(tokens?.IdToken))
            {
                return await ReadIdentityAsync(tokens.IdToken, cancellationToken);
            }

            logger.LogWarning("LinkedIn code exchange succeeded but returned no id_token");
            return null;
        }
    }

    private async Task<LinkedInIdentity?> ReadIdentityAsync(string idToken, CancellationToken cancellationToken)
    {
        var jwks = await jwksProvider.GetAsync(cancellationToken);
        var identity = LinkedInIdTokenReader.Read(idToken, jwks, _options.ClientId!, _timeProvider.GetUtcNow(), out var failure);
        if (identity is not null)
        {
            return identity;
        }

        // A bad signature is the one failure mode a stale JWKS cache can cause on its own (LinkedIn
        // rotated its signing keys since the last fetch) — every other validation failure
        // (issuer/audience/expiry/tampering) would fail identically on a retry, so this costs nothing
        // in the common case and recovers a real key rotation without a redeploy.
        logger.LogInformation("LinkedIn id_token failed validation with the cached JWKS ({Reason}); refreshing keys and retrying", failure);
        var refreshed = await jwksProvider.RefreshAsync(cancellationToken);
        identity = LinkedInIdTokenReader.Read(idToken, refreshed, _options.ClientId!, _timeProvider.GetUtcNow(), out failure);
        if (identity is null)
        {
            // The library's IDX code names the failing check (issuer/audience/expiry/signature) with
            // PII masked — exactly what was missing on 2026-09-05, when the bare "failed validation"
            // line hid an issuer mismatch for an hour.
            logger.LogWarning("LinkedIn id_token failed validation: {Reason}", failure);
        }

        return identity;
    }

    private sealed record TokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

    private sealed record TokenErrorResponse([property: JsonPropertyName("error")] string? Error);
}
