using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AfterApply.Application.JobSearch.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// The typed HttpClient behind <see cref="IJSearchClient"/>. Builds the query string the provider
/// documents, sends the two RapidAPI headers on every request (never on the client's defaults, so
/// the key is not captured at registration), parses both response shapes — the provider's
/// <c>{status, request_id, data | error}</c> envelope and the gateway's bare <c>{message}</c> —
/// and retries once, at most, for what the provider calls transient.
///
/// Nothing here logs the URL or the body: the URL carries the user's search text and the body
/// echoes it back under <c>parameters</c>. Status, request id and remaining quota are enough for
/// a support ticket. The client registration removes HttpClientFactory's default logger for the
/// same reason.
/// </summary>
public sealed class JSearchClient(
    HttpClient httpClient,
    IOptions<JobSearchOptions> options,
    JSearchThrottle throttle,
    ILogger<JSearchClient> logger) : IJSearchClient
{
    private const int MaxAttempts = 2;

    public Task<JSearchResult<JSearchSearchData>> SearchAsync(JSearchSearchRequest request, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder()
            .Add("query", request.Query)
            .Add("cursor", request.Cursor)
            .Add("num_pages", request.NumPages)
            .Add("country", request.Country)
            .Add("language", request.Language)
            .Add("location", request.Location)
            // The provider's own defaults are omitted rather than sent, so the request — and the
            // cache key derived from the same normalised values — is the same whether the caller
            // said "all" or said nothing.
            .Add("date_posted", request.DatePosted is { } dp && dp != JobSearchDatePosted.All ? dp.ToWire() : null)
            .Add("work_from_home", request.WorkFromHome == true ? "true" : null)
            .Add("employment_types", Join(request.EmploymentTypes?.Select(e => e.ToWire())))
            .Add("job_requirements", Join(request.JobRequirements?.Select(r => r.ToWire())))
            .Add("radius", request.Radius)
            .Add("exclude_job_publishers", Join(request.ExcludeJobPublishers))
            .Add("fields", Join(request.Fields));

        return SendAsync<JSearchSearchData>("search-v2", query, cancellationToken);
    }

    public async Task<JSearchResult<IReadOnlyList<JSearchJob>>> GetJobDetailsAsync(JSearchJobDetailsRequest request, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder()
            .Add("job_id", Join(request.JobIds))
            .Add("country", request.Country)
            .Add("language", request.Language)
            .Add("fields", Join(request.Fields));

        var result = await SendAsync<List<JSearchJob>>("job-details", query, cancellationToken);
        return new JSearchResult<IReadOnlyList<JSearchJob>>(result.Data, result.RequestId, result.RateLimit);
    }

    public async Task<JSearchResult<IReadOnlyList<JSearchSalaryEstimate>>> GetEstimatedSalaryAsync(JSearchEstimatedSalaryRequest request, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder()
            .Add("job_title", request.JobTitle)
            .Add("location", request.Location)
            .Add("location_type", request.LocationType != JobSearchLocationType.Any ? request.LocationType.ToWire() : null)
            .Add("years_of_experience", request.YearsOfExperience != JobSearchExperienceRange.All ? request.YearsOfExperience.ToWire() : null)
            .Add("fields", Join(request.Fields));

        var result = await SendAsync<List<JSearchSalaryEstimate>>("estimated-salary", query, cancellationToken);
        return new JSearchResult<IReadOnlyList<JSearchSalaryEstimate>>(result.Data, result.RequestId, result.RateLimit);
    }

    public async Task<JSearchResult<IReadOnlyList<JSearchCompanySalary>>> GetCompanyJobSalaryAsync(JSearchCompanySalaryRequest request, CancellationToken cancellationToken)
    {
        var query = new QueryBuilder()
            .Add("company", request.Company)
            .Add("job_title", request.JobTitle)
            .Add("location", request.Location)
            .Add("location_type", request.LocationType != JobSearchLocationType.Any ? request.LocationType.ToWire() : null)
            .Add("years_of_experience", request.YearsOfExperience != JobSearchExperienceRange.All ? request.YearsOfExperience.ToWire() : null);

        var result = await SendAsync<List<JSearchCompanySalary>>("company-job-salary", query, cancellationToken);
        return new JSearchResult<IReadOnlyList<JSearchCompanySalary>>(result.Data, result.RequestId, result.RateLimit);
    }

    private async Task<JSearchResult<T>> SendAsync<T>(string path, QueryBuilder query, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new JSearchException(JSearchFailure.NotConfigured, null, null, "JobSearch:ApiKey is not configured.");
        }

        var relativeUri = query.Build(path);
        JSearchException? lastFailure = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await throttle.WaitAsync(cancellationToken);
                var result = await SendOnceAsync<T>(relativeUri, settings, cancellationToken);
                return result with { Attempts = attempt };
            }
            catch (JSearchException exception) when (attempt == MaxAttempts || !IsRetryable(exception))
            {
                exception.Attempts = attempt;
                throw;
            }
            catch (JSearchException exception) when (IsRetryable(exception))
            {
                lastFailure = exception;
                logger.LogInformation("JSearch {Path} attempt {Attempt} failed with {Failure} ({StatusCode}); retrying once.",
                    path, attempt, exception.Failure, exception.StatusCode);
                if (settings.RetryDelayMilliseconds > 0)
                {
                    await Task.Delay(settings.RetryDelayMilliseconds, cancellationToken);
                }
            }
        }

        // Unreachable in practice — the loop either returns or rethrows on the last attempt —
        // but the compiler cannot see that.
        throw lastFailure ?? new JSearchException(JSearchFailure.Upstream, null, null, "JSearch call did not complete.");
    }

    /// <summary>
    /// The provider's own advice: retry 5xx and 429 after a short wait. With one exception — a
    /// 429 whose <c>x-ratelimit-requests-remaining</c> is zero is the monthly hard limit, which no
    /// pause of seconds will clear, and a retry there is a wasted round trip at best.
    /// </summary>
    private static bool IsRetryable(JSearchException exception) => exception.Failure switch
    {
        JSearchFailure.Transport => true,
        JSearchFailure.Upstream => exception.StatusCode is null or >= 500,
        JSearchFailure.RateLimited => !exception.Data.Contains(RemainingKey) || exception.Data[RemainingKey] is int and > 0,
        _ => false
    };

    private const string RemainingKey = "remaining";

    private async Task<JSearchResult<T>> SendOnceAsync<T>(string relativeUri, JobSearchOptions settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.TryAddWithoutValidation("x-rapidapi-key", settings.ApiKey);
        request.Headers.TryAddWithoutValidation("x-rapidapi-host", settings.Host);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            throw new JSearchException(JSearchFailure.Transport, null, null, "JSearch could not be reached.");
        }

        using (response)
        {
            var rateLimit = ReadRateLimit(response);
            var status = (int)response.StatusCode;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                logger.LogWarning("JSearch answered {StatusCode} with a non-JSON body.", status);
                throw Classify(response.StatusCode, null, null, rateLimit);
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw Classify(response.StatusCode, null, null, rateLimit);
                }

                var requestId = root.TryGetProperty("request_id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : null;

                if (root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
                {
                    // The provider's own envelope.
                    if (string.Equals(statusElement.GetString(), "OK", StringComparison.OrdinalIgnoreCase)
                        && root.TryGetProperty("data", out var dataElement))
                    {
                        T? data;
                        try
                        {
                            data = dataElement.Deserialize<T>(JSearchWireValues.JsonOptions);
                        }
                        catch (JsonException)
                        {
                            data = default;
                        }

                        if (data is null)
                        {
                            logger.LogWarning("JSearch {RequestId} answered OK with data this build cannot read.", requestId);
                            throw new JSearchException(JSearchFailure.Malformed, status, requestId, "JSearch data had an unexpected shape.");
                        }

                        logger.LogInformation("JSearch {RequestId} OK; {Remaining} requests remaining this period.",
                            requestId, rateLimit.Remaining);
                        return new JSearchResult<T>(data, requestId, rateLimit);
                    }

                    var (code, message) = ReadError(root);
                    logger.LogWarning("JSearch {RequestId} answered status ERROR (http {StatusCode}, code {Code}).",
                        requestId, status, code);
                    var failure = (code ?? status) is >= 400 and < 500 ? JSearchFailure.BadRequest : JSearchFailure.Upstream;
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        failure = JSearchFailure.RateLimited;
                    }

                    throw WithRemaining(new JSearchException(failure, code ?? status, requestId, message ?? "JSearch reported an error."), rateLimit);
                }

                // The gateway's shape: {"message": "..."} with no status field.
                var gatewayMessage = root.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString()
                    : null;
                throw Classify(response.StatusCode, requestId, gatewayMessage, rateLimit);
            }
        }
    }

    private JSearchException Classify(HttpStatusCode statusCode, string? requestId, string? message, JSearchRateLimit rateLimit)
    {
        var status = (int)statusCode;
        var failure = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => JSearchFailure.Unauthorized,
            HttpStatusCode.TooManyRequests => JSearchFailure.RateLimited,
            _ when status >= 500 => JSearchFailure.Upstream,
            _ when status >= 400 => JSearchFailure.Upstream,
            _ => JSearchFailure.Malformed
        };

        if (failure == JSearchFailure.Unauthorized)
        {
            // A configuration problem, not a user one — the key is wrong or the subscription is
            // gone, and every call will fail the same way until someone fixes it.
            logger.LogError("JSearch rejected the RapidAPI key or subscription ({StatusCode}).", status);
        }
        else
        {
            logger.LogWarning("JSearch gateway answered {StatusCode} ({Failure}).", status, failure);
        }

        return WithRemaining(new JSearchException(failure, status, requestId, message ?? $"JSearch answered {status}."), rateLimit);
    }

    private static JSearchException WithRemaining(JSearchException exception, JSearchRateLimit rateLimit)
    {
        if (rateLimit.Remaining is { } remaining)
        {
            exception.Data[RemainingKey] = remaining;
        }

        return exception;
    }

    private static (int? Code, string? Message) ReadError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        int? code = error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number
                    && codeElement.TryGetInt32(out var parsed)
            ? parsed
            : null;
        var message = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : null;
        return (code, message);
    }

    private static JSearchRateLimit ReadRateLimit(HttpResponseMessage response) =>
        new(ReadHeaderInt(response, "x-ratelimit-requests-limit"),
            ReadHeaderInt(response, "x-ratelimit-requests-remaining"),
            ReadHeaderInt(response, "x-ratelimit-requests-reset"));

    private static int? ReadHeaderInt(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
        && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string? Join(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var joined = string.Join(',', values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>Builds <c>path?k=v&amp;k=v</c> with every value percent-encoded and every absent
    /// value left out entirely — the provider treats an empty <c>country=</c> as a value.</summary>
    private sealed class QueryBuilder
    {
        private readonly StringBuilder _builder = new();

        public QueryBuilder Add(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return this;
            }

            _builder.Append(_builder.Length == 0 ? '?' : '&')
                .Append(name)
                .Append('=')
                .Append(Uri.EscapeDataString(value.Trim()));
            return this;
        }

        public QueryBuilder Add(string name, int? value) =>
            value is { } v ? Add(name, v.ToString(CultureInfo.InvariantCulture)) : this;

        public string Build(string path) => path + _builder;
    }
}
