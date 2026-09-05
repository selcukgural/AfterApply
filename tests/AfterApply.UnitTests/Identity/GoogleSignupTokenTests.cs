using System.Security.Cryptography;
using AfterApply.Application.Identity;
using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// The signup token is what proves, on the second request of a Google sign-up, that the identity
// really came from Google — the authorization code is already spent by then. These tests pin the
// properties that make it safe to accept: it round-trips the identity, it can't be forged or
// altered, it expires, and it is never interchangeable with an access token even though both are
// signed with the same key.
public class GoogleSignupTokenTests
{
    private static readonly GoogleIdentity Identity = new("108", "ada@example.com", true, "Ada", "Lovelace");

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

        var token = service.CreateGoogleSignupToken(Identity);
        var restored = service.ValidateGoogleSignupToken(token);

        restored.ShouldBe(Identity);
    }

    [Fact]
    public void Round_Trips_An_Identity_Without_Names()
    {
        var service = Service();
        var identity = Identity with { GivenName = null, FamilyName = null };

        service.ValidateGoogleSignupToken(service.CreateGoogleSignupToken(identity)).ShouldBe(identity);
    }

    [Fact]
    public void Rejects_A_Token_Signed_With_Another_Key()
    {
        var token = Service(signingKey: Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
            .CreateGoogleSignupToken(Identity);

        Service().ValidateGoogleSignupToken(token).ShouldBeNull();
    }

    [Fact]
    public void Rejects_A_Tampered_Token()
    {
        var service = Service();
        var token = service.CreateGoogleSignupToken(Identity);
        var parts = token.Split('.');
        // Flip the payload while keeping the original signature.
        parts[1] = parts[1][..^2] + (parts[1][^2] == 'A' ? "BB" : "AA");

        service.ValidateGoogleSignupToken(string.Join('.', parts)).ShouldBeNull();
    }

    [Fact]
    public void Rejects_An_Expired_Token()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var service = Service(clock);
        var token = service.CreateGoogleSignupToken(Identity);

        clock.Advance(TimeSpan.FromMinutes(9));
        service.ValidateGoogleSignupToken(token).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromMinutes(2));
        service.ValidateGoogleSignupToken(token).ShouldBeNull();
    }

    [Fact]
    public void An_Access_Token_Is_Not_A_Signup_Token()
    {
        var service = Service();
        var (accessToken, _) = service.CreateAccessToken(Guid.NewGuid(), "ada@example.com");

        service.ValidateGoogleSignupToken(accessToken).ShouldBeNull();
    }

    [Fact]
    public void Garbage_Is_Rejected_Not_Thrown()
    {
        Service().ValidateGoogleSignupToken("nope").ShouldBeNull();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
