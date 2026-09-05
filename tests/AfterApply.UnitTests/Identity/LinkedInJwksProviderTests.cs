using System.Net;
using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// Fetch/cache behaviour only — LinkedInIdTokenReaderTests covers what the keys are used for. A
// canned, syntactically-valid-but-not-cryptographically-meaningful JWKS body is enough here: nothing
// in this file ever verifies a signature against it.
public class LinkedInJwksProviderTests
{
    private const string CannedJwks =
        """{"keys":[{"kty":"RSA","kid":"key-1","use":"sig","alg":"RS256","n":"sXchDaQebHnPiGvyDOAT4saGEUi5ji","e":"AQAB"}]}""";

    [Fact]
    public async Task Fetches_Once_And_Serves_The_Cache_On_A_Second_Call()
    {
        var handler = new CountingHandler(CannedJwks);
        var provider = new LinkedInJwksProvider(new HttpClient(handler));

        var first = await provider.GetAsync(CancellationToken.None);
        var second = await provider.GetAsync(CancellationToken.None);

        handler.CallCount.ShouldBe(1);
        first.Keys.Single().KeyId.ShouldBe("key-1");
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task Refetches_Once_The_Cache_Has_Expired()
    {
        var handler = new CountingHandler(CannedJwks);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var provider = new LinkedInJwksProvider(new HttpClient(handler), clock);

        await provider.GetAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(23));
        await provider.GetAsync(CancellationToken.None);
        handler.CallCount.ShouldBe(1);

        clock.Advance(TimeSpan.FromHours(2));
        await provider.GetAsync(CancellationToken.None);
        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshAsync_Always_Refetches_Regardless_Of_Cache_Age()
    {
        var handler = new CountingHandler(CannedJwks);
        var provider = new LinkedInJwksProvider(new HttpClient(handler));

        await provider.GetAsync(CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);

        handler.CallCount.ShouldBe(3);
    }

    private sealed class CountingHandler(string responseBody) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody) });
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
