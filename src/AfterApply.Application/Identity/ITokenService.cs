namespace AfterApply.Application.Identity;

public interface ITokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Guid userId, string email);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    string GeneratePersonalAccessToken();

    string HashPersonalAccessToken(string token);

    /// <summary>Short-lived, signed carrier for a verified Google identity between the two steps of a
    /// Google sign-up (see GoogleSignupPrefill): the authorization code is single-use and already
    /// spent by the time the user sees the consent form, so this is what proves, on the second
    /// request, that the identity really came from Google.</summary>
    string CreateGoogleSignupToken(GoogleIdentity identity);

    /// <summary>Null for anything that is not an unexpired token from
    /// <see cref="CreateGoogleSignupToken"/> — including one of our own access tokens. Async only
    /// because the JWT library's validation entry point is; creating a token stays synchronous.</summary>
    Task<GoogleIdentity?> ValidateGoogleSignupTokenAsync(string token);

    /// <summary>Same role as <see cref="CreateGoogleSignupToken"/>, for a LinkedIn sign-up. A
    /// separate pair of methods rather than a shared generic one: <see cref="LinkedInIdentity"/>'s
    /// email is nullable (LinkedIn's OpenID Connect response makes it optional) where
    /// <see cref="GoogleIdentity"/>'s is not, so the two claim sets aren't quite interchangeable.</summary>
    string CreateLinkedInSignupToken(LinkedInIdentity identity);

    /// <summary>Null for anything that is not an unexpired token from
    /// <see cref="CreateLinkedInSignupToken"/> — including one of our own access tokens.</summary>
    Task<LinkedInIdentity?> ValidateLinkedInSignupTokenAsync(string token);
}
