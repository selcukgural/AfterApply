using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace AfterApply.IntegrationTests.RateLimits;

/// <summary>
/// The rate limits count in Redis (DECISIONS.md 2026-09-18): one window per caller across every
/// API instance, not one per instance. Hosts here opt back into rate limiting (the suite turns it
/// off for everything else, see TestContainerCleanup) as Standalone hosts, because a fixed window
/// has no reset within a test class. TestServer reports no client address, so every request is the
/// same anonymous caller — exactly the shape the assertions need.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RedisRateLimitTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The bug this exists for: before, five failed logins on instance A and five on
    /// instance B were ten, because each instance counted its own window.</summary>
    [Fact]
    public async Task One_Window_Spans_Two_Instances()
    {
        await using var instanceA = host.Standalone(RateLimitingOn);
        await using var instanceB = host.Standalone(RateLimitingOn);
        var a = instanceA.CreateClient();
        var b = instanceB.CreateClient();

        // auth-strict is 5 per minute per IP. Three on A, two on B: the window is full.
        for (var i = 0; i < 3; i++)
        {
            (await FailedLoginAsync(a)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        for (var i = 0; i < 2; i++)
        {
            (await FailedLoginAsync(b)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var sixthOnA = await FailedLoginAsync(a);
        sixthOnA.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        // The Redis lease reports its retry-after in whole seconds under its own metadata name;
        // the header has to come out of it all the same.
        sixthOnA.Headers.RetryAfter.ShouldNotBeNull();
        sixthOnA.Headers.RetryAfter!.Delta!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        sixthOnA.Headers.RetryAfter.Delta.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(60));

        (await FailedLoginAsync(b)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Two_Policies_Do_Not_Share_One_Callers_Window()
    {
        await using var limited = host.Standalone(RateLimitingOn);
        var client = limited.CreateClient();

        // Spend the auth-strict window (5/min per IP) ...
        for (var i = 0; i < 5; i++)
        {
            await FailedLoginAsync(client);
        }

        (await FailedLoginAsync(client)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // ... and the same address is still welcome on a differently-limited anonymous route
        // (60/min per IP on the public directory). The library keys a window on the partition
        // alone, so this only holds because the policy name is part of the Redis key.
        (await client.GetAsync("/api/companies/public/")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A Redis outage must degrade to the old per-instance window, not to a 500 on every
    /// rate-limited endpoint and not to an open door.</summary>
    [Fact]
    public async Task Without_Redis_The_Window_Is_Counted_Locally()
    {
        await using var withoutRedis = host.Standalone(builder =>
        {
            RateLimitingOn(builder);
            builder.UseSetting("ConnectionStrings:Redis", "localhost:1,abortConnect=false,connectTimeout=300,syncTimeout=300");
            builder.UseSetting("Redis:WaitForBackplaneSubscribe", "false");
        });
        var client = withoutRedis.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await FailedLoginAsync(client);
        }

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        last.Headers.RetryAfter.ShouldNotBeNull();
    }

    private static void RateLimitingOn(IWebHostBuilder builder) => builder.UseSetting("RateLimiting:Enabled", "true");

    private static Task<HttpResponseMessage> FailedLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest("no-such-user@example.com", "whatever"), JsonOptions);

    /// <summary>A server-side render names the visitor it renders for; with the render key the API
    /// counts each visitor on their own, without it every such request is the web service's one
    /// address (2026-09-24, ClientPartition).</summary>
    [Fact]
    public async Task A_Render_With_The_Key_Counts_Each_Visitor_Apart_And_Without_It_Does_Not()
    {
        await using var limited = host.Standalone(builder =>
        {
            RateLimitingOn(builder);
            builder.UseSetting("RateLimiting:ServerRenderKey", "test-render-key");
        });
        var client = limited.CreateClient();

        Task<HttpResponseMessage> AsVisitor(string? key, string visitor)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("no-such-user@example.com", "whatever"), options: JsonOptions)
            };
            if (key is not null)
            {
                request.Headers.Add("X-Render-Key", key);
            }

            request.Headers.Add("X-Render-Client", visitor);
            return client.SendAsync(request);
        }

        for (var i = 0; i < 5; i++)
        {
            (await AsVisitor("test-render-key", "198.51.100.1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await AsVisitor("test-render-key", "198.51.100.1")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await AsVisitor("test-render-key", "198.51.100.2")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // A wrong key names nobody: all such requests share the connection's bucket.
        for (var i = 0; i < 5; i++)
        {
            (await AsVisitor("guessed", $"203.0.113.{i}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await AsVisitor("guessed", "203.0.113.99")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
