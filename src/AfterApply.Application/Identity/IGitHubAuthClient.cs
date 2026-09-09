namespace AfterApply.Application.Identity;

/// <summary>
/// The one thing e-kariyerim needs from GitHub for "Sign in with GitHub": turn the authorization
/// code the browser brought back from github.com into a verified identity. Same port shape as
/// <see cref="ILinkedInAuthClient"/> — the exchange (client secret, token endpoint, the two GitHub
/// REST calls that follow it) lives behind this so the API layer and the integration tests never
/// talk to GitHub; tests swap in a fake that maps codes to identities.
/// </summary>
public interface IGitHubAuthClient
{
    /// <summary>Returns null when GitHub rejects the code (already used, expired, wrong redirect
    /// URI) or the profile it hands back is unusable — every one of those is "the sign-in did not
    /// happen", not an exception the caller could act on. Throws only for a missing configuration,
    /// which is a deployment error.</summary>
    Task<GitHubIdentity?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken);
}

/// <summary>
/// What the sign-in flow acts on after a GitHub exchange. <see cref="Subject"/> is GitHub's numeric
/// user id rendered as a string — never the login/username, which the owner can change at will and
/// which GitHub then frees for someone else to take.
///
/// Two things differ from <see cref="LinkedInIdentity"/> even though the shape matches. GitHub has
/// no OpenID Connect id_token, so these fields come from <c>GET /user</c> + <c>GET /user/emails</c>
/// rather than from signed claims — the TLS channel to api.github.com is what vouches for them.
/// And GitHub stores one free-text <c>name</c>, not a given/family pair, so
/// <see cref="GivenName"/>/<see cref="FamilyName"/> are a best-effort split the user can correct on
/// the sign-up form (see <c>GitHubProfileReader</c>). <see cref="Email"/> is null whenever GitHub
/// exposed no address we can both verify and deliver to, and the emailless sign-up path — already
/// built for LinkedIn — takes over.
/// </summary>
public sealed record GitHubIdentity(
    string Subject,
    string? Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName);
