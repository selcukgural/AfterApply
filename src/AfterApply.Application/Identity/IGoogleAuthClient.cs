namespace AfterApply.Application.Identity;

/// <summary>
/// The one thing e-kariyerim needs from Google for "Sign in with Google": turn the authorization
/// code the browser brought back from accounts.google.com into a verified identity. The exchange
/// itself (client secret, token endpoint, ID-token checks) lives behind this port so the API layer
/// and the integration tests never talk to Google — tests swap in a fake that maps codes to
/// identities.
/// </summary>
public interface IGoogleAuthClient
{
    /// <summary>Returns null when Google rejects the code (already used, expired, wrong redirect
    /// URI, PKCE verifier mismatch) or the returned ID token fails validation — every one of those
    /// is "the sign-in did not happen", not an exception the caller could do anything about.
    /// Throws only for a missing configuration, which is a deployment error.</summary>
    Task<GoogleIdentity?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken);
}

/// <summary>The claims from a Google ID token that the sign-in flow acts on. <see cref="Subject"/>
/// is Google's stable account id — what gets stored as the external login key, never the email,
/// which a Google user can change.</summary>
public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName);
