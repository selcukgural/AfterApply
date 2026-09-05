using System.Security.Cryptography;
using System.Text;
using AfterApply.Application.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider? timeProvider = null) : ITokenService
{
    // A different audience from the access token is what keeps the two from ever being confused:
    // JwtBearer rejects a signup token on aud alone, and ValidateGoogleSignupToken rejects an
    // access token the same way — regardless of both being signed with the same key.
    private const string GoogleSignupAudience = "AfterApply.GoogleSignup";
    private const string LinkedInSignupAudience = "AfterApply.LinkedInSignup";
    private const string PurposeClaim = "purpose";
    private const string GoogleSignupPurpose = "google-signup";
    private const string LinkedInSignupPurpose = "linkedin-signup";
    private static readonly TimeSpan GoogleSignupTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LinkedInSignupTokenLifetime = TimeSpan.FromMinutes(10);

    private readonly JwtOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Guid userId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Email] = email
            }
        };

        var handler = new JsonWebTokenHandler();
        var accessToken = handler.CreateToken(descriptor);

        return (accessToken, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string refreshToken)
    {
        return Hash(refreshToken);
    }

    public string GeneratePersonalAccessToken()
    {
        // Base64Url (not plain Base64) so the token is safe to paste as-is into the extension's
        // options page or an Authorization header without accidental '+'/'/' encoding surprises.
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return PersonalAccessTokenDefaults.TokenPrefix + random;
    }

    public string HashPersonalAccessToken(string token)
    {
        return Hash(token);
    }

    public string CreateGoogleSignupToken(GoogleIdentity identity)
    {
        var now = _timeProvider.GetUtcNow();
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = identity.Subject,
            [JwtRegisteredClaimNames.Email] = identity.Email,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [PurposeClaim] = GoogleSignupPurpose,
            ["email_verified"] = identity.EmailVerified
        };
        if (identity.GivenName is not null)
        {
            claims[JwtRegisteredClaimNames.GivenName] = identity.GivenName;
        }

        if (identity.FamilyName is not null)
        {
            claims[JwtRegisteredClaimNames.FamilyName] = identity.FamilyName;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = GoogleSignupAudience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(GoogleSignupTokenLifetime).UtcDateTime,
            SigningCredentials = SigningCredentials(),
            Claims = claims
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public GoogleIdentity? ValidateGoogleSignupToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        var result = handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = GoogleSignupAudience,
            IssuerSigningKey = SigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (_, expires, _, _) => expires is not null && expires > _timeProvider.GetUtcNow().UtcDateTime
        }).GetAwaiter().GetResult();

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            return null;
        }

        if (!jwt.TryGetPayloadValue<string>(PurposeClaim, out var purpose) || purpose != GoogleSignupPurpose)
        {
            return null;
        }

        var email = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Email, out var e) ? e : null;
        if (string.IsNullOrEmpty(jwt.Subject) || string.IsNullOrEmpty(email))
        {
            return null;
        }

        var emailVerified = jwt.TryGetPayloadValue<bool>("email_verified", out var verified) && verified;
        var givenName = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.GivenName, out var g) ? g : null;
        var familyName = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.FamilyName, out var f) ? f : null;

        return new GoogleIdentity(jwt.Subject, email, emailVerified, givenName, familyName);
    }

    public string CreateLinkedInSignupToken(LinkedInIdentity identity)
    {
        var now = _timeProvider.GetUtcNow();
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = identity.Subject,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [PurposeClaim] = LinkedInSignupPurpose,
            ["email_verified"] = identity.EmailVerified
        };
        // Omitted entirely (not written as null/empty) when LinkedIn gave no email — the reader
        // below distinguishes "claim absent" from "claim present but empty" the same way.
        if (identity.Email is not null)
        {
            claims[JwtRegisteredClaimNames.Email] = identity.Email;
        }

        if (identity.GivenName is not null)
        {
            claims[JwtRegisteredClaimNames.GivenName] = identity.GivenName;
        }

        if (identity.FamilyName is not null)
        {
            claims[JwtRegisteredClaimNames.FamilyName] = identity.FamilyName;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = LinkedInSignupAudience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(LinkedInSignupTokenLifetime).UtcDateTime,
            SigningCredentials = SigningCredentials(),
            Claims = claims
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    // TODO: WHY WE'RE BLOCKING THREAD?
    public LinkedInIdentity? ValidateLinkedInSignupToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        var result = handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = LinkedInSignupAudience,
            IssuerSigningKey = SigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (_, expires, _, _) => expires is not null && expires > _timeProvider.GetUtcNow().UtcDateTime
        }).GetAwaiter().GetResult();

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            return null;
        }

        if (!jwt.TryGetPayloadValue<string>(PurposeClaim, out var purpose) || purpose != LinkedInSignupPurpose)
        {
            return null;
        }

        if (string.IsNullOrEmpty(jwt.Subject))
        {
            return null;
        }

        // Unlike ValidateGoogleSignupToken, a missing email here is not itself a rejection reason —
        // LinkedIn legitimately sends no email for some accounts, and the sign-up flow handles that.
        var email = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Email, out var e) ? e : null;
        var emailVerified = jwt.TryGetPayloadValue<bool>("email_verified", out var verified) && verified;
        var givenName = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.GivenName, out var g) ? g : null;
        var familyName = jwt.TryGetPayloadValue<string>(JwtRegisteredClaimNames.FamilyName, out var f) ? f : null;

        return new LinkedInIdentity(jwt.Subject, email, emailVerified, givenName, familyName);
    }

    private SymmetricSecurityKey SigningKey() => new(Convert.FromBase64String(_options.SigningKey));

    private SigningCredentials SigningCredentials() => new(SigningKey(), SecurityAlgorithms.HmacSha256);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
