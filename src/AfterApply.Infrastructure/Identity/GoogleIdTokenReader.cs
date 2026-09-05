using AfterApply.Application.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Reads the claims e-kariyerim needs out of a Google ID token and rejects one that was not issued
/// by Google, for us, and is still valid.
///
/// No signature check, on purpose: <see cref="GoogleAuthClient"/> only ever feeds this the ID token
/// it just received in the token endpoint's response, over TLS, in a server-to-server call — the
/// case OpenID Connect Core §3.1.3.7 (step 6) explicitly allows to skip signature validation for,
/// because the transport already proves the token came from Google. What a forged token could not
/// fake is being in that response. The issuer/audience/expiry checks below are the ones the spec
/// does still require. If a second source of ID tokens ever appears (e.g. Google's JavaScript
/// "One Tap" button posting a token from the browser), that source needs full JWKS validation and
/// must not be routed through here.
/// </summary>
public static class GoogleIdTokenReader
{
    // Google issues tokens under both spellings; both are documented as valid.
    private static readonly string[] AcceptedIssuers = ["https://accounts.google.com", "accounts.google.com"];

    public static GoogleIdentity? Read(string idToken, string expectedClientId, DateTimeOffset now)
    {
        JsonWebToken token;
        try
        {
            token = new JsonWebToken(idToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (!AcceptedIssuers.Contains(token.Issuer, StringComparer.Ordinal))
        {
            return null;
        }

        if (!token.Audiences.Contains(expectedClientId, StringComparer.Ordinal))
        {
            return null;
        }

        // ValidTo is DateTime.MinValue when the claim is missing; a token with no expiry is rejected too.
        if (token.ValidTo == DateTime.MinValue || token.ValidTo <= now.UtcDateTime)
        {
            return null;
        }

        var subject = token.Subject;
        var email = Claim(token, "email");
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
        {
            return null;
        }

        return new GoogleIdentity(
            subject,
            email,
            ReadEmailVerified(token),
            NullIfEmpty(Claim(token, "given_name")),
            NullIfEmpty(Claim(token, "family_name")));
    }

    // Google emits a JSON boolean, but the claim is read as its string form here; some IdPs (and
    // some older Google docs) show it as the string "true", so both spellings count as verified.
    private static bool ReadEmailVerified(JsonWebToken token)
    {
        var value = Claim(token, "email_verified");
        return bool.TryParse(value, out var verified) && verified;
    }

    private static string? Claim(JsonWebToken token, string name) =>
        token.TryGetPayloadValue<string>(name, out var value) ? value : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
