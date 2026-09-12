using System.Net;
using AfterApply.Infrastructure.JobSearch;
using Shouldly;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>One retry, and only for what a pause of seconds could fix. A 429 with nothing left
/// in the month is not that, and a second attempt there is a wasted round trip at best.</summary>
public class JSearchClientRetryTests
{
    private static readonly string Ok = JSearchFixtures.Read("search-v2.json");

    [Fact]
    public async Task A_503_Followed_By_A_200_Succeeds_After_One_Retry()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, """{"message":"down"}""").Enqueue(HttpStatusCode.OK, Ok);

        var result = await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        result.Data.Jobs!.Count.ShouldBe(2);
        result.Attempts.ShouldBe(2);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Two_503s_Fail_After_Exactly_Two_Attempts()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "{}").Enqueue(HttpStatusCode.ServiceUnavailable, "{}");

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.Upstream);
        exception.Attempts.ShouldBe(2);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_Transport_Failure_Is_Retried_Once()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.EnqueueThrow(new HttpRequestException("reset")).Enqueue(HttpStatusCode.OK, Ok);

        await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_429_With_Quota_Remaining_Is_Retried_Once()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.TooManyRequests, """{"message":"Too many requests"}""", ("x-ratelimit-requests-remaining", "5"))
            .Enqueue(HttpStatusCode.OK, Ok);

        await client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None);

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_429_With_Nothing_Remaining_Is_Not_Retried()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.TooManyRequests, """{"message":"Too many requests"}""", ("x-ratelimit-requests-remaining", "0"))
            .Enqueue(HttpStatusCode.OK, Ok);

        var exception = await Should.ThrowAsync<JSearchException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        exception.Failure.ShouldBe(JSearchFailure.RateLimited);
        exception.Attempts.ShouldBe(1);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_400_Is_Never_Retried()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.BadRequest, JSearchFixtures.Read("error-envelope.json")).Enqueue(HttpStatusCode.OK, Ok);

        await Should.ThrowAsync<JSearchException>(() => client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_403_Is_Never_Retried()
    {
        var (client, handler) = JSearchTestClient.Create();
        handler.Enqueue(HttpStatusCode.Forbidden, JSearchFixtures.Read("gateway-403.json")).Enqueue(HttpStatusCode.OK, Ok);

        await Should.ThrowAsync<JSearchException>(() => client.SearchAsync(new JSearchSearchRequest("x"), CancellationToken.None));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_Callers_Cancellation_Is_Not_Turned_Into_A_Retry()
    {
        var (client, handler) = JSearchTestClient.Create();
        using var cts = new CancellationTokenSource();
        handler.EnqueueThrow(new OperationCanceledException(cts.Token)).Enqueue(HttpStatusCode.OK, Ok);
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            client.SearchAsync(new JSearchSearchRequest("x"), cts.Token));

        handler.Requests.Count.ShouldBe(1);
    }
}
