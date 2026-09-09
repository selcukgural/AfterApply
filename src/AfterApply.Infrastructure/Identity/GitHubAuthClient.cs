using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// The server half of "Sign in with GitHub": exchanges the code the browser brought back for an
/// access token, then reads the identity behind it with two GitHub REST calls.
///
/// Unlike Google and LinkedIn there is no OpenID Connect here at all — GitHub's OAuth Apps issue no
/// id_token, so there is no signed assertion to verify and no JWKS to fetch. What vouches for the
/// identity is the authenticated TLS channel to api.github.com, the same trust
/// <see cref="GoogleIdTokenReader"/> settles for. The access token is used for the two reads and
/// then dropped: it is never stored, never logged and never returned to the caller, and the scopes
/// the web app asks for (<c>read:user user:email</c>) can do nothing but answer "who is this".
///
/// No PKCE: GitHub's OAuth App endpoints don't take a code_verifier. The browser's single-use
/// <c>state</c> is the login-CSRF defence (see the web app's githubOAuth.ts).
/// </summary>
internal sealed class GitHubAuthClient(
    HttpClient httpClient,
    IOptions<GitHubAuthOptions> options,
    ILogger<GitHubAuthClient> logger) : IGitHubAuthClient
{
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string ProfileEndpoint = "https://api.github.com/user";
    private const string EmailsEndpoint = "https://api.github.com/user/emails";

    private readonly GitHubAuthOptions _options = options.Value;

    public async Task<GitHubIdentity?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "GitHubAuth:ClientId / GitHubAuth:ClientSecret are not configured. For local dev run " +
                "'dotnet user-secrets set GitHubAuth:ClientId \"...\" --project src/AfterApply.Api' (and ClientSecret), " +
                "or set GitHubAuth__ClientId / GitHubAuth__ClientSecret in the environment.");
        }

        var accessToken = await ExchangeForAccessTokenAsync(code, redirectUri, cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        var profile = await GetAsync<ProfileResponse>(ProfileEndpoint, accessToken, cancellationToken);
        if (profile is null || profile.Id <= 0)
        {
            logger.LogWarning("GitHub code exchange succeeded but the profile could not be read");
            return null;
        }

        // A null list (not an empty one) means the addresses could not be read at all — most likely
        // the user granted the app without the user:email scope, which GitHub allows. That is not a
        // failed sign-in: GitHubProfileReader turns it into "no usable email", and the sign-up form
        // asks for one. GET /user's own `email` field is deliberately not used as a fallback — it
        // carries no verification flag, so it could not be trusted for account matching anyway.
        var emails = await GetAsync<List<EmailResponse>>(EmailsEndpoint, accessToken, cancellationToken);

        return GitHubProfileReader.Read(
            new GitHubProfile(profile.Id, profile.Login, profile.Name),
            emails?.Select(e => new GitHubEmail(e.Email ?? string.Empty, e.Primary, e.Verified)).ToList());
    }

    private async Task<string?> ExchangeForAccessTokenAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = _options.ClientId!,
                ["client_secret"] = _options.ClientSecret!
            })
        };
        // Without this GitHub answers the token endpoint in form-urlencoded, not JSON.
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "GitHub token endpoint unreachable");
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub code exchange rejected: {Status}", (int)response.StatusCode);
                return null;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            // GitHub answers a spent/expired code or a redirect_uri mismatch with HTTP 200 and an
            // error object, not a 4xx — so a status check alone would happily hand back a null token.
            if (!string.IsNullOrEmpty(tokens?.Error))
            {
                logger.LogWarning("GitHub code exchange rejected: {Error}", tokens.Error);
                return null;
            }

            if (string.IsNullOrEmpty(tokens?.AccessToken))
            {
                logger.LogWarning("GitHub code exchange returned no access token");
                return null;
            }

            return tokens.AccessToken;
        }
    }

    private async Task<T?> GetAsync<T>(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        // api.github.com rejects a request without a User-Agent outright, and pins the response
        // shape to a dated API version rather than "whatever is current".
        request.Headers.UserAgent.ParseAdd("e-kariyerim-signin");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // The URL and status only — never the token, and never the body, which for /user is
                // the caller's own profile.
                logger.LogWarning("GitHub {Url} returned {Status}", url, (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "GitHub {Url} unreachable", url);
            return default;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record ProfileResponse(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record EmailResponse(
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("verified")] bool Verified);
}
