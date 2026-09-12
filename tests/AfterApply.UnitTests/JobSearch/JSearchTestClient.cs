using System.Net;
using AfterApply.Infrastructure.JobSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AfterApply.UnitTests.JobSearch;

/// <summary>A JSearchClient over a scripted handler: each queued response answers one request,
/// in order; the handler records every request it saw.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public ScriptedHandler Enqueue(HttpStatusCode status, string body, params (string Name, string Value)[] headers)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            foreach (var (name, value) in headers)
            {
                response.Headers.TryAddWithoutValidation(name, value);
            }

            return response;
        });
        return this;
    }

    public ScriptedHandler EnqueueThrow(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No scripted response left for " + request.RequestUri);
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}

internal static class JSearchTestClient
{
    public static JobSearchOptions Options(Action<JobSearchOptionsBuilder>? configure = null)
    {
        var builder = new JobSearchOptionsBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    public static (JSearchClient Client, ScriptedHandler Handler) Create(JobSearchOptions? options = null)
    {
        options ??= Options();
        var handler = new ScriptedHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        var throttle = new JSearchThrottle(Microsoft.Extensions.Options.Options.Create(options));
        var client = new JSearchClient(httpClient, Microsoft.Extensions.Options.Options.Create(options), throttle,
            NullLogger<JSearchClient>.Instance);
        return (client, handler);
    }

    public sealed class JobSearchOptionsBuilder
    {
        public bool Enabled { get; set; } = true;
        public string? ApiKey { get; set; } = "test-key";
        public int RetryDelayMilliseconds { get; set; }
        public int RequestsPerSecond { get; set; }
        public string DefaultCountry { get; set; } = "tr";

        public JobSearchOptions Build() => new()
        {
            Enabled = Enabled,
            ApiKey = ApiKey,
            RetryDelayMilliseconds = RetryDelayMilliseconds,
            RequestsPerSecond = RequestsPerSecond,
            DefaultCountry = DefaultCountry
        };
    }
}
