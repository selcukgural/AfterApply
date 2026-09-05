using AfterApply.Infrastructure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// GoogleIdTokenReader deliberately skips signature validation (the token comes straight from
// Google's token endpoint over TLS — see the class doc), so the issuer/audience/expiry checks are
// the entire defence against a token that was minted for someone else. Each one gets pinned here,
// with tokens built unsigned exactly the way a malicious or misrouted one would be.
public class GoogleIdTokenReaderTests
{
    private const string ClientId = "1234.apps.googleusercontent.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reads_The_Identity_Out_Of_A_Valid_Token()
    {
        var token = Token(claims: new Dictionary<string, object>
        {
            ["sub"] = "10769150350006150715113082367",
            ["email"] = "ada@example.com",
            ["email_verified"] = true,
            ["given_name"] = "Ada",
            ["family_name"] = "Lovelace"
        });

        var identity = GoogleIdTokenReader.Read(token, ClientId, Now);

        identity.ShouldNotBeNull();
        identity.Subject.ShouldBe("10769150350006150715113082367");
        identity.Email.ShouldBe("ada@example.com");
        identity.EmailVerified.ShouldBeTrue();
        identity.GivenName.ShouldBe("Ada");
        identity.FamilyName.ShouldBe("Lovelace");
    }

    [Theory]
    [InlineData("https://accounts.google.com")]
    [InlineData("accounts.google.com")]
    public void Accepts_Both_Issuer_Spellings_Google_Uses(string issuer)
    {
        GoogleIdTokenReader.Read(Token(issuer: issuer), ClientId, Now).ShouldNotBeNull();
    }

    [Fact]
    public void Rejects_A_Token_From_Another_Issuer()
    {
        GoogleIdTokenReader.Read(Token(issuer: "https://accounts.example.com"), ClientId, Now).ShouldBeNull();
    }

    [Fact]
    public void Rejects_A_Token_Issued_For_Another_Client()
    {
        GoogleIdTokenReader.Read(Token(audience: "someone-else.apps.googleusercontent.com"), ClientId, Now).ShouldBeNull();
    }

    [Fact]
    public void Rejects_An_Expired_Token()
    {
        var token = Token(expires: Now.AddMinutes(-1));

        GoogleIdTokenReader.Read(token, ClientId, Now).ShouldBeNull();
        // Sanity: the same token is fine when read before it expired.
        GoogleIdTokenReader.Read(token, ClientId, Now.AddMinutes(-2)).ShouldNotBeNull();
    }

    [Fact]
    public void Rejects_A_Token_Without_Subject_Or_Email()
    {
        GoogleIdTokenReader.Read(Token(claims: new Dictionary<string, object> { ["email"] = "x@example.com" }), ClientId, Now)
            .ShouldBeNull();
        GoogleIdTokenReader.Read(Token(claims: new Dictionary<string, object> { ["sub"] = "1" }), ClientId, Now)
            .ShouldBeNull();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Email_Verified_Is_Read_From_Its_String_Form_Too(string value, bool expected)
    {
        var token = Token(claims: new Dictionary<string, object>
        {
            ["sub"] = "1", ["email"] = "x@example.com", ["email_verified"] = value
        });

        GoogleIdTokenReader.Read(token, ClientId, Now)!.EmailVerified.ShouldBe(expected);
    }

    [Fact]
    public void Missing_Email_Verified_Means_Not_Verified()
    {
        var token = Token(claims: new Dictionary<string, object> { ["sub"] = "1", ["email"] = "x@example.com" });

        GoogleIdTokenReader.Read(token, ClientId, Now)!.EmailVerified.ShouldBeFalse();
    }

    [Fact]
    public void Missing_Names_Come_Back_As_Null_Not_Empty()
    {
        var token = Token(claims: new Dictionary<string, object> { ["sub"] = "1", ["email"] = "x@example.com" });

        var identity = GoogleIdTokenReader.Read(token, ClientId, Now)!;
        identity.GivenName.ShouldBeNull();
        identity.FamilyName.ShouldBeNull();
    }

    [Fact]
    public void Garbage_Is_Rejected_Not_Thrown()
    {
        GoogleIdTokenReader.Read("not-a-jwt", ClientId, Now).ShouldBeNull();
    }

    private static string Token(
        string issuer = "https://accounts.google.com",
        string audience = ClientId,
        DateTimeOffset? expires = null,
        IDictionary<string, object>? claims = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = Now.AddMinutes(-5).UtcDateTime,
            Expires = (expires ?? Now.AddHours(1)).UtcDateTime,
            // No SigningCredentials: an unsigned ("alg":"none") token, which is the point.
            Claims = claims ?? new Dictionary<string, object>
            {
                ["sub"] = "1", ["email"] = "x@example.com", ["email_verified"] = true
            }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
