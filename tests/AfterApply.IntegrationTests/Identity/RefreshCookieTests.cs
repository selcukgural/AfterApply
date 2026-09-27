using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

public sealed class RefreshCookieProfile : IHostProfile
{
    public const string WebOrigin = "https://web.ekariyerim.test";

    public void Configure(IWebHostBuilder builder) => builder.UseSetting("Cors:AllowedOrigins:0", WebOrigin);

    public void Reset()
    {
    }
}

/// <summary>
/// The web session's refresh token lives in an HttpOnly cookie and never in a response body
/// (DECISIONS.md 2026-09-27): what the cookie looks like, how it rotates when two tabs race for it,
/// when it stops working, and who may spend it.
/// </summary>
public class RefreshCookieTests(ApiHost<RefreshCookieProfile> host)
    : IClassFixture<ApiHost<RefreshCookieProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sign_In_Puts_The_Refresh_Token_In_A_Locked_Down_Cookie_And_Not_In_The_Body()
    {
        var (client, _) = await host.RegisterAsync("cookie.flags@example.com");

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("cookie.flags@example.com", ApiHost.DefaultPassword), JsonOptions);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadAsStringAsync();
        body.ShouldContain("accessToken");
        body.ShouldNotContain("refreshToken", Case.Insensitive);

        var setCookie = RefreshCookie.SetCookieHeader(login);
        setCookie.ShouldNotBeNull();
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/auth", Case.Insensitive);
        setCookie.ShouldNotContain("domain=", Case.Insensitive);
    }

    [Fact]
    public async Task Refresh_Rotates_The_Cookie_And_Returns_No_Token_In_The_Body()
    {
        var (client, auth) = await host.RegisterAsync("cookie.rotate@example.com");

        var refreshed = await client.RefreshAsync(auth.RefreshToken);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await refreshed.Content.ReadAsStringAsync()).ShouldNotContain("refreshToken", Case.Insensitive);
        var next = RefreshCookie.From(refreshed);
        next.ShouldNotBeNull();
        next.ShouldNotBe(auth.RefreshToken);
        (await client.RefreshAsync(next)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_Without_A_Cookie_Is_Unauthorized()
    {
        var response = await host.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>A browser signed in before the cookie existed still holds its token in localStorage;
    /// it trades it for the cookie once instead of being signed out.</summary>
    [Fact]
    public async Task A_Token_From_The_Body_Is_Traded_For_The_Cookie()
    {
        var (client, auth) = await host.RegisterAsync("cookie.legacy@example.com");

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken), JsonOptions);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        RefreshCookie.From(refreshed).ShouldNotBeNull();
    }

    /// <summary>Two tabs share one cookie. The one that loses the race must not look like a replay —
    /// that would revoke every session of the account.</summary>
    [Fact]
    public async Task Two_Refreshes_Racing_With_One_Cookie_Leave_The_Session_Alive()
    {
        var (client, auth) = await host.RegisterAsync("cookie.race@example.com");

        var responses = await Task.WhenAll(client.RefreshAsync(auth.RefreshToken), client.RefreshAsync(auth.RefreshToken));

        responses.Select(r => r.StatusCode).ShouldNotContain(HttpStatusCode.Unauthorized);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeGreaterThanOrEqualTo(1);
        var winner = RefreshCookie.From(responses.First(r => r.StatusCode == HttpStatusCode.OK))!;

        // The loser's replay of the old cookie is still a race, not a theft...
        var late = await client.RefreshAsync(auth.RefreshToken);
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        RefreshCookie.SetCookieHeader(late).ShouldBeNull();

        // ...so the winner's cookie keeps working.
        (await client.RefreshAsync(winner)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Rotated_Token_Replayed_After_The_Grace_Window_Revokes_Every_Session()
    {
        var (client, auth) = await host.RegisterAsync("cookie.replay@example.com");
        var refreshed = await client.RefreshAsync(auth.RefreshToken);
        var current = RefreshCookie.From(refreshed)!;
        await host.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.UserId == auth.User.Id && t.RevokedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow.AddMinutes(-5))));

        var replay = await client.RefreshAsync(auth.RefreshToken);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        RefreshCookie.SetCookieHeader(replay).ShouldNotBeNull("a dead cookie is cleared");
        (await client.RefreshAsync(current)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Session_Ends_At_Its_Absolute_Lifetime_However_Often_It_Was_Refreshed()
    {
        var (client, auth) = await host.RegisterAsync("cookie.absolute@example.com");
        var current = RefreshCookie.From(await client.RefreshAsync(auth.RefreshToken))!;
        var startedAt = await host.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.UserId == auth.User.Id).Select(t => t.SessionStartedAt).Distinct().ToListAsync());
        startedAt.Count.ShouldBe(1, "rotation carries the session start forward");

        await host.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.UserId == auth.User.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SessionStartedAt, DateTimeOffset.UtcNow.AddDays(-91))));

        (await client.RefreshAsync(current)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Foreign_Origin_Cannot_Spend_The_Cookie()
    {
        var (client, auth) = await host.RegisterAsync("cookie.origin@example.com");

        (await client.RefreshAsync(auth.RefreshToken, origin: "https://evil.example")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.RefreshAsync(auth.RefreshToken, origin: RefreshCookieProfile.WebOrigin)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_Revokes_The_Cookie_And_Clears_It()
    {
        var (client, auth) = await host.RegisterAsync("cookie.logout@example.com");

        var logout = await client.LogoutAsync(auth.RefreshToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        RefreshCookie.SetCookieHeader(logout)!.ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);
        (await client.RefreshAsync(auth.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Only /api/auth answers a credentialed request: that is where the cookie is set and
    /// read. Everything else stays Bearer-only.</summary>
    [Fact]
    public async Task Only_The_Auth_Routes_Allow_Credentialed_Cross_Origin_Requests()
    {
        var client = host.CreateClient();

        var auth = await client.SendAsync(Preflight("/api/auth/refresh"));
        auth.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([RefreshCookieProfile.WebOrigin]);
        auth.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);

        var other = await client.SendAsync(Preflight("/api/applications"));
        other.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([RefreshCookieProfile.WebOrigin]);
        other.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();

        static HttpRequestMessage Preflight(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, path);
            request.Headers.Add("Origin", RefreshCookieProfile.WebOrigin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type");
            return request;
        }
    }
}
