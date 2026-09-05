namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// "Sign in with Google" configuration, bound from the <c>GoogleAuth</c> section. Both values come
/// from an OAuth 2.0 client of type "Web application" in Google Cloud Console; the client id is
/// public (it ends up in the browser's redirect to accounts.google.com), the secret is not.
///
/// Same "inert until set" pattern as <c>ResendOptions</c>/<c>OpenAiOptions</c>: while either value
/// is missing, <see cref="IsConfigured"/> is false, <c>GET /api/config</c> reports the feature as
/// disabled so the web app never shows the button, and the two <c>/api/auth/google*</c> endpoints
/// answer 404. Nothing fails at startup, so a deployment without a Google client keeps working.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>The login-provider key stored in AspNetUserLogins for a linked Google account.</summary>
    public const string LoginProvider = "Google";

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
