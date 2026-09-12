using System.Diagnostics;
using System.Net;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// Fetches LinkedIn's public job-search and job-posting fragments the way a logged-out browser
/// would, and nothing more: plain GET, honest User-Agent, no cookies, no JavaScript. The same
/// technique the link preview and company enrichment already use, with two differences the
/// sweep needs — every response becomes an outcome (see <see cref="ILinkedInJobSourceClient"/>),
/// and 429/403/999 or a redirect into <c>/authwall</c> or <c>/login</c> is reported as the
/// source saying stop, which the sweep obeys for a day.
///
/// The URLs come from <see cref="LinkedInJobSearchUrlBuilder"/> only, redirects are followed by
/// hand (the handler has <c>AllowAutoRedirect=false</c>) and only to <c>*.linkedin.com</c>, and
/// the body is read up to a cap — a search page is ~30 KB and a posting ~35 KB.
/// </summary>
public sealed class LinkedInJobSourceClient(HttpClient httpClient, IOptions<JobSourceOptions> options) : ILinkedInJobSourceClient
{
    private const int MaxRedirectHops = 5;
    private const int MaxBodyChars = 200_000;
    private const string LinkedInHost = "linkedin.com";
    private const HttpStatusCode LinkedInRequestDenied = (HttpStatusCode)999;

    public Task<JobSourceFetchResult<IReadOnlyList<JobSourceCard>>> SearchAsync(JobSourceQuery query, int start, CancellationToken cancellationToken)
    {
        var uri = LinkedInJobSearchUrlBuilder.Search(query.Keywords, query.Location, query.TimeWindow, query.RemoteOnly, start);
        return FetchAsync(uri, html => (IReadOnlyList<JobSourceCard>)LinkedInJobSearchCardParser.Parse(html), cancellationToken);
    }

    public Task<JobSourceFetchResult<JobSourceDetail>> GetPostingAsync(string externalId, CancellationToken cancellationToken)
    {
        var uri = LinkedInJobSearchUrlBuilder.Posting(externalId);
        return FetchAsync(uri, LinkedInJobPostingParser.Parse, cancellationToken);
    }

    private async Task<JobSourceFetchResult<T>> FetchAsync<T>(Uri uri, Func<string, T> parse, CancellationToken cancellationToken)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var currentUri = uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var status = response.StatusCode;

                if (IsRedirect(status) && response.Headers.Location is { } location)
                {
                    var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    if (!IsLinkedIn(nextUri) || IsWall(nextUri))
                    {
                        // Off-site, or onto the login wall: both mean "not for you".
                        return Result<T>(JobSourceFetchOutcome.Blocked, status, stopwatch);
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var html = await ReadCappedAsync(response, cancellationToken);
                    return Result(JobSourceFetchOutcome.Ok, status, stopwatch, parse(html));
                }

                return Result<T>(status switch
                {
                    HttpStatusCode.TooManyRequests => JobSourceFetchOutcome.RateLimited,
                    HttpStatusCode.Forbidden or LinkedInRequestDenied => JobSourceFetchOutcome.Blocked,
                    _ => JobSourceFetchOutcome.Error
                }, status, stopwatch);
            }

            return Result<T>(JobSourceFetchOutcome.Error, null, stopwatch);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TimeoutRejectedException or BrokenCircuitException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Transport trouble, the pipeline giving up, or the in-process breaker being open: all
            // "try again next time", none "the source said no". A cancellation the caller asked
            // for is not caught — it propagates as it should.
            return Result<T>(JobSourceFetchOutcome.Error, null, stopwatch);
        }
    }

    private static JobSourceFetchResult<T> Result<T>(JobSourceFetchOutcome outcome, HttpStatusCode? status, Stopwatch stopwatch,
        T? value = null) where T : class =>
        new(outcome, status is null ? null : (int)status, (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue), value);

    private static async Task<string> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[MaxBodyChars];
        var read = await reader.ReadBlockAsync(buffer, cancellationToken);
        return new string(buffer, 0, read);
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsLinkedIn(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals(LinkedInHost, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + LinkedInHost, StringComparison.OrdinalIgnoreCase));

    private static bool IsWall(Uri uri) =>
        uri.AbsolutePath.StartsWith("/authwall", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/uas/login", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/checkpoint", StringComparison.OrdinalIgnoreCase);
}
