using System.Security.Cryptography;
using System.Text;
using AfterApply.Application.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure.Identity;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

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

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
