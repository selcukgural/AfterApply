using System.Net;

namespace AfterApply.IntegrationTests.Feedback;

/// <summary>
/// Stands in for api.github.com so the suite never opens a real issue anywhere. Only the transport
/// is replaced — the real mirror, its repository-slug check, the composer and the write-back all
/// still run, which is the point of covering this at integration level.
/// </summary>
internal sealed class StubGitHubHandler(HttpStatusCode statusCode = HttpStatusCode.Created) : HttpMessageHandler
{
    private readonly List<(Uri Uri, string Body, string? Authorization)> _requests = [];

    public IReadOnlyList<(Uri Uri, string Body, string? Authorization)> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (_requests)
        {
            _requests.Add((request.RequestUri!, body, request.Headers.Authorization?.ToString()));
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                """{"number":412,"html_url":"https://github.com/owner/repo/issues/412"}""",
                System.Text.Encoding.UTF8, "application/json")
        };
    }
}
