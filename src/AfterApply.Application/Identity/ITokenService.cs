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
    /// <see cref="CreateGoogleSignupToken"/> — including one of our own access tokens.</summary>
    GoogleIdentity? ValidateGoogleSignupToken(string token);
}
