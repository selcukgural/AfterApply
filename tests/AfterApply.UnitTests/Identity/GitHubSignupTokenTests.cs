using System.Security.Cryptography;
using AfterApply.Application.Identity;
using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// Same guarantees as LinkedInSignupTokenTests: the identity survives the round-trip (including a
// missing email as null, not an empty string), and a signup token minted for one provider can never
// be spent on another's endpoint even though all three are signed with the same key.
public class GitHubSignupTokenTests
{
    private static readonly GitHubIdentity Identity = new("4241", "ada@example.com", true, "Augusta Ada", "King");
    private static readonly GitHubIdentity EmaillessIdentity = new("4242", null, false, "Grace", "Hopper");

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
    public async Task Round_Trips_The_Identity()
    {
        var service = Service();

        var token = service.CreateGitHubSignupToken(Identity);
        var restored = await service.ValidateGitHubSignupTokenAsync(token);

        restored.ShouldBe(Identity);
    }

    [Fact]
    public async Task Round_Trips_An_Identity_With_No_Email_As_Null_Not_Empty()
    {
        var service = Service();

        var token = service.CreateGitHubSignupToken(EmaillessIdentity);
        var restored = await service.ValidateGitHubSignupTokenAsync(token);

        restored.ShouldBe(EmaillessIdentity);
        restored!.Email.ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_A_Token_Signed_With_Another_Key()
    {
        var token = Service(signingKey: Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
            .CreateGitHubSignupToken(Identity);

        (await Service().ValidateGitHubSignupTokenAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_A_Tampered_Token()
    {
        var service = Service();
        var token = service.CreateGitHubSignupToken(Identity);
        var parts = token.Split('.');
        parts[1] = parts[1][..^2] + (parts[1][^2] == 'A' ? "BB" : "AA");

        (await service.ValidateGitHubSignupTokenAsync(string.Join('.', parts))).ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_An_Expired_Token()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
        var service = Service(clock);
        var token = service.CreateGitHubSignupToken(Identity);

        clock.Advance(TimeSpan.FromMinutes(9));
        (await service.ValidateGitHubSignupTokenAsync(token)).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromMinutes(2));
        (await service.ValidateGitHubSignupTokenAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task Another_Provider_Signup_Token_Is_Not_A_GitHub_Signup_Token()
    {
        var service = Service();
        var googleToken = service.CreateGoogleSignupToken(new GoogleIdentity("g1", "g@example.com", true, "G", "H"));
        var linkedInToken = service.CreateLinkedInSignupToken(new LinkedInIdentity("li-1", "l@example.com", true, "L", "K"));

        (await service.ValidateGitHubSignupTokenAsync(googleToken)).ShouldBeNull();
        (await service.ValidateGitHubSignupTokenAsync(linkedInToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_GitHub_Signup_Token_Is_Not_Accepted_By_The_Other_Providers()
    {
        var service = Service();
        var token = service.CreateGitHubSignupToken(Identity);

        (await service.ValidateGoogleSignupTokenAsync(token)).ShouldBeNull();
        (await service.ValidateLinkedInSignupTokenAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task An_Access_Token_Is_Not_A_Signup_Token()
    {
        var service = Service();
        var (accessToken, _) = service.CreateAccessToken(Guid.NewGuid(), "ada@example.com");

        (await service.ValidateGitHubSignupTokenAsync(accessToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Garbage_Is_Rejected_Not_Thrown()
    {
        (await Service().ValidateGitHubSignupTokenAsync("nope")).ShouldBeNull();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
