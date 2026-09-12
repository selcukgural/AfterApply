using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>
/// Stands in for jsearch.p.rapidapi.com. Only the transport is replaced — the real client, its
/// URL building, envelope parsing and retry, and the whole service (settings, cache, ledger,
/// ceilings) still run, which is the point of covering this at integration level. Routes by path
/// to the canned playground bodies; <c>job-details</c> is synthesised per requested id so a
/// batch of three comes back as three postings (minus any id in <see cref="UnknownJobIds"/>).
/// </summary>
internal sealed class StubJSearchHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];
    private readonly Queue<(HttpStatusCode Status, string Body, int? Remaining)> _overrides = new();

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public int CallCount => Requests.Count;

    /// <summary>The body served for <c>/search-v2</c>; defaults to the US playground sample.</summary>
    public string SearchBody { get; set; } = JSearchFixtures.Read("search-v2.json");

    /// <summary>Ids the stub pretends the provider no longer knows.</summary>
    public HashSet<string> UnknownJobIds { get; } = [];

    /// <summary>Value of <c>x-ratelimit-requests-remaining</c> on every normal answer; null omits it.</summary>
    public int? RemainingHeader { get; set; } = 150;

    /// <summary>Queue a one-off answer for the next call, whatever its path.</summary>
    public StubJSearchHandler AnswerNext(HttpStatusCode status, string body, int? remaining = null)
    {
        _overrides.Enqueue((status, body, remaining));
        return this;
    }

    public IReadOnlyList<Uri> UrisFor(string path) => Requests.Where(r => r.RequestUri!.AbsolutePath == path).Select(r => r.RequestUri!).ToList();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        if (_overrides.Count > 0)
        {
            var (status, body, remaining) = _overrides.Dequeue();
            return Task.FromResult(Build(status, body, remaining));
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
        var response = request.RequestUri.AbsolutePath switch
        {
            "/search-v2" => SearchBody,
            "/job-details" => DetailsFor(query.TryGetValue("job_id", out var ids) ? ids.ToString() : string.Empty),
            "/estimated-salary" => JSearchFixtures.Read("estimated-salary.json"),
            "/company-job-salary" => JSearchFixtures.Read("company-job-salary.json"),
            _ => null
        };

        return Task.FromResult(response is null
            ? Build(HttpStatusCode.NotFound, """{"message":"Endpoint '/x' does not exist"}""", RemainingHeader)
            : Build(HttpStatusCode.OK, response, RemainingHeader));
    }

    private string DetailsFor(string ids)
    {
        var template = JsonNode.Parse(JSearchFixtures.Read("job-details.json"))!.AsObject();
        var sample = template["data"]!.AsArray()[0]!.AsObject();
        var data = new JsonArray();
        foreach (var id in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (UnknownJobIds.Contains(id))
            {
                continue;
            }

            var job = JsonNode.Parse(sample.ToJsonString())!.AsObject();
            job["job_id"] = id;
            job["job_title"] = $"Detail for {id}";
            data.Add(job);
        }

        template["data"] = data;
        return template.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static HttpResponseMessage Build(HttpStatusCode status, string body, int? remaining)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("x-ratelimit-requests-limit", "200");
        if (remaining is { } value)
        {
            response.Headers.TryAddWithoutValidation("x-ratelimit-requests-remaining", value.ToString());
        }

        return response;
    }
}
