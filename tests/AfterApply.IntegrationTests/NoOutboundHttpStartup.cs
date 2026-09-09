using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

[assembly: HostingStartup(typeof(AfterApply.IntegrationTests.NoOutboundHttpStartup))]

namespace AfterApply.IntegrationTests;

/// <summary>
/// Cuts the wire: no host this assembly builds can make a real outbound HTTP request.
/// </summary>
/// <remarks>
/// A test must never talk to the internet, and this suite did. The enrichment job fetches the
/// LinkedIn/kariyer.net company page of whatever company a test happens to create, so a run printed
/// <c>GET https://www.linkedin.com/company/legacy-ext-corp/</c> from
/// <c>ExtensionApplicationTests</c> — a class that stubs nothing and does not know it is making
/// network calls. That is wrong three times over: it asserts nothing (whatever comes back is
/// whatever LinkedIn feels like returning today), it makes the suite's outcome depend on someone
/// else's uptime and rate limiting, and it is the shape of stall that leaves a run sitting with a
/// live Hangfire server and no test progress. The same door had already let something worse
/// through: the feedback mirror opened five real GitHub issues from a test run on 2026-09-07,
/// because <c>WebApplicationFactory</c> boots the real <c>Program</c> and therefore loads the API
/// project's user secrets — a developer's live tokens included.
/// <para>
/// So the default is now "blocked", and a test that wants HTTP has to say so. Every client whose
/// primary handler could actually open a socket gets it replaced by
/// <see cref="BlockedOutboundHttpHandler" />;
/// a test that needs a response stubs that one client with
/// <c>ConfigurePrimaryHttpMessageHandler</c>, whose per-client action runs after this filter and so
/// wins for exactly that client (see CompanyEnrichmentTests, the Feedback mirror tests and
/// ResendEmailSenderTests). Anything not stubbed throws with a message naming the client, instead
/// of quietly reaching a real host.
/// </para>
/// <para>
/// Delivered as an <see cref="IHostingStartup" /> rather than per test class on purpose: a run
/// builds a host per test — around 200 of them across 33 classes — and a rule that each class has
/// to remember to opt into is a rule that lasts until the next class is written. The
/// <c>ASPNETCORE_HOSTINGSTARTUPASSEMBLIES</c> variable that loads this is set from a module
/// initializer in TestContainerCleanup, so it applies to every host in the assembly, including the
/// classes that configure nothing at all. NoOutboundHttpTests is the guard that it is actually in
/// force.
/// </para>
/// <para>
/// This covers clients built by <c>IHttpClientFactory</c>, which is how all of this codebase's own
/// outbound HTTP is registered. An SDK that news up its own <c>HttpClient</c> internally (the
/// OpenAI client does) cannot be reached this way, so those stay switched off by configuration
/// instead — see TestContainerCleanup.
/// </para>
/// </remarks>
public sealed class NoOutboundHttpStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, BlockOutboundHttpFilter>());
}

internal sealed class BlockOutboundHttpFilter : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) =>
        builder =>
        {
            // next() first, then decide. The per-client actions from ConfigurePrimaryHttpMessageHandler
            // run inside next(), and production code uses them too — the enrichment and job-preview
            // clients both set `new HttpClientHandler { AllowAutoRedirect = false }` there, so a
            // handler installed before next() was overwritten and those two clients (the ones that
            // fetch a URL out of user data, and the ones that were reaching LinkedIn) went on dialling
            // out. Running last is the only way to have the final word.
            next(builder);

            // Replace only a handler that can actually open a socket. That distinguishes production's
            // own primary handler from a test's stub without either side having to register anything:
            // a stub is some other HttpMessageHandler, so it survives and that client behaves exactly
            // as its test intends.
            if (builder.PrimaryHandler is HttpClientHandler or SocketsHttpHandler)
            {
                builder.PrimaryHandler = new BlockedOutboundHttpHandler(builder.Name ?? "(unnamed)");
            }
        };
}

internal sealed class BlockedOutboundHttpHandler(string clientName) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw Blocked(request);

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw Blocked(request);

    // An HttpRequestException, not a custom type: to the code under test this has to look like a
    // host it could not reach, which is a case the fetching services already handle (enrichment
    // and the link preview both treat it as "no data") rather than a new failure mode invented by
    // the test harness.
    private HttpRequestException Blocked(HttpRequestMessage request) =>
        new($"Integration tests do not make real outbound HTTP requests. The '{clientName}' client " +
            $"tried to reach {request.Method} {request.RequestUri}. Stub that client for this test with " +
            ".ConfigurePrimaryHttpMessageHandler(...) — see NoOutboundHttpStartup.");
}
