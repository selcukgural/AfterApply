namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// "Sign in with LinkedIn" configuration, bound from the <c>LinkedInAuth</c> section. Both values
/// come from a LinkedIn Developer app (with the "Sign In with LinkedIn using OpenID Connect" product
/// enabled) tied to a LinkedIn Page; the client id is public (it ends up in the browser's redirect
/// to linkedin.com), the secret is not.
///
/// Same "inert until set" pattern as <c>GoogleAuthOptions</c>/<c>ResendOptions</c>/<c>OpenAiOptions</c>:
/// while either value is missing, <see cref="IsConfigured"/> is false, <c>GET /api/config</c> reports
/// the feature as disabled so the web app never shows the button, and the two <c>/api/auth/linkedin*</c>
/// endpoints answer 404. Nothing fails at startup, so a deployment without a LinkedIn client keeps
/// working.
/// </summary>
public sealed class LinkedInAuthOptions
{
    public const string SectionName = "LinkedInAuth";

    /// <summary>The login-provider key stored in AspNetUserLogins for a linked LinkedIn account.</summary>
    public const string LoginProvider = "LinkedIn";

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
