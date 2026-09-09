namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// "Sign in with GitHub" configuration, bound from the <c>GitHubAuth</c> section. Both values come
/// from a GitHub OAuth App (Settings → Developer settings → OAuth Apps — not a GitHub App, which is
/// the installable-on-repositories kind and is not what a login needs); the client id is public (it
/// ends up in the browser's redirect to github.com), the secret is not.
///
/// Same "inert until set" pattern as <c>GoogleAuthOptions</c>/<c>LinkedInAuthOptions</c>: while
/// either value is missing, <see cref="IsConfigured"/> is false, <c>GET /api/config</c> reports the
/// feature as disabled so the web app never shows the button, and the two <c>/api/auth/github*</c>
/// endpoints answer 404. Nothing fails at startup, so a deployment without a GitHub client keeps
/// working.
/// </summary>
public sealed class GitHubAuthOptions
{
    public const string SectionName = "GitHubAuth";

    /// <summary>The login-provider key stored in AspNetUserLogins for a linked GitHub account.</summary>
    public const string LoginProvider = "GitHub";

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
