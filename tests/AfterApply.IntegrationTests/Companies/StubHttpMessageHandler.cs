using System.Net;

namespace AfterApply.IntegrationTests.Companies;

/// <summary>
/// Serves canned responses to the enrichment HttpClient so the suite never reaches the real
/// internet. This matters beyond speed: CompanyEnrichmentService's fetch is queued through
/// Hangfire, whose server really does run in these tests, so without this a test that supplies a
/// company-profile URL would fire an outbound request at linkedin.com or kariyer.net from
/// whatever machine ran the suite.
///
/// Only the transport is replaced — the real service, its host allow-list and the real parsers all
/// still run, which is the whole point of exercising this at integration level rather than
/// stubbing the service away.
/// </summary>
internal sealed class StubHttpMessageHandler(IReadOnlyDictionary<string, string> responsesByHost) : HttpMessageHandler
{
    private readonly List<Uri> _requested = [];

    public IReadOnlyList<Uri> Requested
    {
        get
        {
            lock (_requested)
            {
                return [.. _requested];
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (_requested)
        {
            _requested.Add(uri);
        }

        var match = responsesByHost.FirstOrDefault(pair =>
            uri.Host.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + pair.Key, StringComparison.OrdinalIgnoreCase));

        // An unmatched host answers 404 rather than throwing: the service treats that as "nothing
        // to enrich from" and leaves the row alone, which is the behaviour a test asserting "we
        // did NOT fetch this" wants to see.
        var response = match.Value is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(match.Value) };

        return Task.FromResult(response);
    }
}
