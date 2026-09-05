using System.Security.Cryptography;
using AfterApply.Application.Identity;
using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// Same guarantees as GoogleSignupTokenTests, plus what's unique to LinkedIn: an identity with no
// email must round-trip that absence faithfully (not as an empty string), and the two providers'
// signup tokens must never be interchangeable even though both share the same signing key.
public class LinkedInSignupTokenTests
{
    private static readonly LinkedInIdentity Identity = new("li-108", "ada@example.com", true, "Ada", "Lovelace");
    private static readonly LinkedInIdentity EmaillessIdentity = new("li-109", null, false, "Grace", "Hopper");

    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static JwtOptions Options(string? signingKey = null) => new()
    {
        SigningKey = signingKey ?? SigningKey,
        Issuer = "AfterApply",
        Audience = "AfterApply.Api",
        AccessTokenMinutes = 20,
        RefreshTokenDays = 30
    };

    private static JwtTokenService Service(TimeProvider? time = null, string? signingKey = null) =>
        new(Microsoft.Extensions.Options.Options.Create(Options(signingKey)), time);

    [Fact]
    public void Round_Trips_The_Identity()
    {
        var service = Service();

        var token = service.CreateLinkedInSignupToken(Identity);
        var restored = service.ValidateLinkedInSignupToken(token);

        restored.ShouldBe(Identity);
    }

    [Fact]
    public void Round_Trips_An_Identity_With_No_Email_As_Null_Not_Empty()
    {
        var service = Service();

        var token = service.CreateLinkedInSignupToken(EmaillessIdentity);
        var restored = service.ValidateLinkedInSignupToken(token);

        restored.ShouldBe(EmaillessIdentity);
        restored!.Email.ShouldBeNull();
    }

    [Fact]
    public void Rejects_A_Token_Signed_With_Another_Key()
    {
        var token = Service(signingKey: Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
            .CreateLinkedInSignupToken(Identity);

        Service().ValidateLinkedInSignupToken(token).ShouldBeNull();
    }

    [Fact]
    public void Rejects_A_Tampered_Token()
    {
        var service = Service();
        var token = service.CreateLinkedInSignupToken(Identity);
        var parts = token.Split('.');
        parts[1] = parts[1][..^2] + (parts[1][^2] == 'A' ? "BB" : "AA");

        service.ValidateLinkedInSignupToken(string.Join('.', parts)).ShouldBeNull();
    }

    [Fact]
    public void Rejects_An_Expired_Token()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var service = Service(clock);
        var token = service.CreateLinkedInSignupToken(Identity);

        clock.Advance(TimeSpan.FromMinutes(9));
        service.ValidateLinkedInSignupToken(token).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromMinutes(2));
        service.ValidateLinkedInSignupToken(token).ShouldBeNull();
    }

    [Fact]
    public void A_Google_Signup_Token_Is_Not_A_LinkedIn_Signup_Token()
    {
        var service = Service();
        var googleToken = service.CreateGoogleSignupToken(new GoogleIdentity("g1", "g@example.com", true, "G", "H"));

        service.ValidateLinkedInSignupToken(googleToken).ShouldBeNull();
    }

    [Fact]
    public void An_Access_Token_Is_Not_A_Signup_Token()
    {
        var service = Service();
        var (accessToken, _) = service.CreateAccessToken(Guid.NewGuid(), "ada@example.com");

        service.ValidateLinkedInSignupToken(accessToken).ShouldBeNull();
    }

    [Fact]
    public void Garbage_Is_Rejected_Not_Thrown()
    {
        Service().ValidateLinkedInSignupToken("nope").ShouldBeNull();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
