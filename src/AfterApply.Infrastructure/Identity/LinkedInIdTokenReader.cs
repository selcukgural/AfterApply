using AfterApply.Application.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Reads the claims e-kariyerim needs out of a LinkedIn ID token and rejects one that was not
/// signed by a key LinkedIn currently publishes, issued by LinkedIn, for us, and still valid.
///
/// Unlike <see cref="GoogleIdTokenReader"/>, this DOES perform full signature validation against
/// LinkedIn's JWKS (<see cref="LinkedInJwksProvider"/>) — a deliberate, more conservative choice for
/// LinkedIn than the "trust the TLS channel" shortcut Google's ID token gets, made explicitly when
/// this feature was added (see DECISIONS.md). <paramref name="jwks"/> is passed in rather than
/// fetched here so this stays a pure, easily testable function.
/// </summary>
public static class LinkedInIdTokenReader
{
    private const string Issuer = "https://www.linkedin.com";

    public static LinkedInIdentity? Read(string idToken, JsonWebKeySet jwks, string expectedClientId, DateTimeOffset now)
    {
        var handler = new JsonWebTokenHandler();
        TokenValidationResult result;
        try
        {
            result = handler.ValidateTokenAsync(idToken, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = expectedClientId,
                IssuerSigningKeys = jwks.Keys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromMinutes(2),
                LifetimeValidator = (_, expires, _, _) => expires is not null && expires > now.UtcDateTime
            }).GetAwaiter().GetResult();
        }
        catch (ArgumentException)
        {
            // A malformed token string ("not a JWT at all") throws rather than failing validation.
            return null;
        }

        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            return null;
        }

        var subject = token.Subject;
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        return new LinkedInIdentity(
            subject,
            NullIfEmpty(Claim(token, "email")),
            ReadEmailVerified(token),
            NullIfEmpty(Claim(token, "given_name")),
            NullIfEmpty(Claim(token, "family_name")));
    }

    // Read as a string first (same reasoning as GoogleIdTokenReader): OIDC's boolean claim can come
    // back either as a JSON boolean or, from some issuers, its string form — both count.
    private static bool ReadEmailVerified(JsonWebToken token)
    {
        var value = Claim(token, "email_verified");
        return bool.TryParse(value, out var verified) && verified;
    }

    private static string? Claim(JsonWebToken token, string name) =>
        token.TryGetPayloadValue<string>(name, out var value) ? value : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
