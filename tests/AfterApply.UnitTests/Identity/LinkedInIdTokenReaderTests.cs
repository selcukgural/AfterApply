using System.Security.Cryptography;
using AfterApply.Infrastructure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// Unlike GoogleIdTokenReader, LinkedInIdTokenReader DOES verify the signature — these tests build a
// real RSA keypair, sign tokens the way LinkedIn's token endpoint would, and hand over the public
// half in a JsonWebKeySet the way LinkedInJwksProvider would.
public class LinkedInIdTokenReaderTests
{
    private const string ClientId = "test-linkedin-client";
    private const string Issuer = "https://www.linkedin.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RSA _otherRsa = RSA.Create(2048);

    [Fact]
    public async Task Reads_The_Identity_Out_Of_A_Validly_Signed_Token()
    {
        var token = Token(claims: new Dictionary<string, object>
        {
            ["sub"] = "linkedin-member-1",
            ["email"] = "ada@example.com",
            ["email_verified"] = true,
            ["given_name"] = "Ada",
            ["family_name"] = "Lovelace"
        });

        var identity = (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now)).Identity;

        identity.ShouldNotBeNull();
        identity.Subject.ShouldBe("linkedin-member-1");
        identity.Email.ShouldBe("ada@example.com");
        identity.EmailVerified.ShouldBeTrue();
        identity.GivenName.ShouldBe("Ada");
        identity.FamilyName.ShouldBe("Lovelace");
    }

    [Fact]
    public async Task Reads_A_Token_With_No_Email_At_All()
    {
        var token = Token(claims: new Dictionary<string, object> { ["sub"] = "linkedin-member-2" });

        var identity = (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now)).Identity!;

        identity.Email.ShouldBeNull();
        identity.EmailVerified.ShouldBeFalse();
        identity.GivenName.ShouldBeNull();
        identity.FamilyName.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_A_Token_Signed_With_A_Key_Not_In_The_Jwks()
    {
        var token = Token(signingKey: _otherRsa);

        (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_A_Tampered_Token()
    {
        var token = Token();
        var parts = token.Split('.');
        parts[1] = parts[1][..^2] + (parts[1][^2] == 'A' ? "BB" : "AA");

        (await LinkedInIdTokenReader.ReadAsync(string.Join('.', parts), Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    [Fact]
    public async Task Accepts_The_Issuer_LinkedIn_Actually_Emits()
    {
        // The live discovery document and real ID tokens say ".../oauth"; the docs page says the bare
        // host. Both must work — the bare-host-only version rejected every real token on 2026-09-05.
        (await LinkedInIdTokenReader.ReadAsync(Token(issuer: "https://www.linkedin.com/oauth"), Jwks(), ClientId, Now)).Identity.ShouldNotBeNull();
        (await LinkedInIdTokenReader.ReadAsync(Token(issuer: "https://www.linkedin.com"), Jwks(), ClientId, Now)).Identity.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rejects_A_Token_From_Another_Issuer()
    {
        var (identity, failure) = await LinkedInIdTokenReader.ReadAsync(Token(issuer: "https://accounts.example.com"), Jwks(), ClientId, Now);
        identity.ShouldBeNull();
        // The reason is surfaced for the server log (IDX10205 = issuer validation failed), never to the client.
        failure.ShouldNotBeNull();
        failure.ShouldContain("IDX10205");
    }

    [Fact]
    public async Task Rejects_A_Lookalike_Issuer_Under_The_Same_Host()
    {
        (await LinkedInIdTokenReader.ReadAsync(Token(issuer: "https://www.linkedin.com/oauth/evil"), Jwks(), ClientId, Now)).Identity.ShouldBeNull();
        (await LinkedInIdTokenReader.ReadAsync(Token(issuer: "https://www.linkedin.com.evil.example"), Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_A_Token_Issued_For_Another_Client()
    {
        (await LinkedInIdTokenReader.ReadAsync(Token(audience: "someone-else"), Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_An_Expired_Token()
    {
        var token = Token(expires: Now.AddMinutes(-1));

        (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now)).Identity.ShouldBeNull();
        // Sanity: the same token is fine when read before it expired.
        (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now.AddMinutes(-2))).Identity.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rejects_A_Token_Without_A_Subject()
    {
        var token = Token(claims: new Dictionary<string, object> { ["email"] = "x@example.com" });

        (await LinkedInIdTokenReader.ReadAsync(token, Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    [Fact]
    public async Task Garbage_Is_Rejected_Not_Thrown()
    {
        (await LinkedInIdTokenReader.ReadAsync("not-a-jwt", Jwks(), ClientId, Now)).Identity.ShouldBeNull();
    }

    private JsonWebKeySet Jwks(string kid = "test-kid")
    {
        var securityKey = new RsaSecurityKey(_rsa) { KeyId = kid };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        jwk.KeyId = kid;
        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        return jwks;
    }

    private string Token(
        string issuer = Issuer,
        string audience = ClientId,
        DateTimeOffset? expires = null,
        RSA? signingKey = null,
        string kid = "test-kid",
        IDictionary<string, object>? claims = null)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(signingKey ?? _rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = Now.AddMinutes(-5).UtcDateTime,
            Expires = (expires ?? Now.AddHours(1)).UtcDateTime,
            SigningCredentials = credentials,
            Claims = claims ?? new Dictionary<string, object>
            {
                ["sub"] = "linkedin-member-1", ["email"] = "x@example.com", ["email_verified"] = true
            }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
