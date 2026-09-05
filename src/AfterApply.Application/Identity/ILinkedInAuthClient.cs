namespace AfterApply.Application.Identity;

/// <summary>
/// The one thing e-kariyerim needs from LinkedIn for "Sign in with LinkedIn": turn the authorization
/// code the browser brought back from linkedin.com into a verified identity. The exchange itself
/// (client secret, token endpoint, ID-token signature/claim checks) lives behind this port so the
/// API layer and the integration tests never talk to LinkedIn — tests swap in a fake that maps codes
/// to identities.
/// </summary>
public interface ILinkedInAuthClient
{
    /// <summary>Returns null when LinkedIn rejects the code (already used, expired, wrong redirect
    /// URI) or the returned ID token fails validation (signature, issuer, audience, expiry) —
    /// every one of those is "the sign-in did not happen", not an exception the caller could do
    /// anything about. Throws only for a missing configuration, which is a deployment error.</summary>
    Task<LinkedInIdentity?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken);
}

/// <summary>The claims from a LinkedIn ID token that the sign-in flow acts on. <see cref="Subject"/>
/// is LinkedIn's stable member id — what gets stored as the external login key, never the email.
/// Unlike Google, LinkedIn's OpenID Connect response makes <see cref="Email"/>/<see cref="EmailVerified"/>
/// genuinely optional (its own documentation: "may not be included in all responses") — the sign-in
/// flow has an explicit path for a null email.</summary>
public sealed record LinkedInIdentity(
    string Subject,
    string? Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName);
