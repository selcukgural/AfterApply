using System.Net;
using System.Text;
using AfterApply.Application.Common;
using AfterApply.Application.JobLiveness;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.AtsSources;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AfterApply.Infrastructure.JobLiveness;

/// <summary>Looks once at one posting and says whether it is still up.</summary>
public interface IJobLivenessClient
{
    /// <param name="url">The stored posting URL — only the ATS path uses it (Workday derives its
    /// API path from it, behind the ATS client's own host checks).</param>
    Task<PostingLiveness> CheckAsync(Source source, string externalId, string? url, CancellationToken cancellationToken);
}

/// <summary>
/// Plain GET with an honest User-Agent, no cookies, no JavaScript — the way every server-side
/// fetch here works. Never throws: a failure is <see cref="PostingLiveness.Unknown"/>, a site
/// saying stop is <see cref="PostingLiveness.Stop"/>.
/// </summary>
/// <remarks>
/// SSRF: the LinkedIn and kariyer.net addresses are built here from the posting's numeric id, never
/// taken from the stored URL, and redirects are followed by hand (the handler has
/// <c>AllowAutoRedirect=false</c>) and only within the same site. It has to be by hand for a second
/// reason: both sites say "closed" in the redirect itself, before its target is ever fetched.
/// </remarks>
public sealed class JobLivenessClient(HttpClient httpClient, IAtsJobClient atsJobClient, IOptions<JobLivenessOptions> options,
    TimeProvider? timeProvider = null)
    : IJobLivenessClient
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private const int MaxRedirectHops = 5;
    private const HttpStatusCode LinkedInRequestDenied = (HttpStatusCode)999;

    public async Task<PostingLiveness> CheckAsync(Source source, string externalId, string? url, CancellationToken cancellationToken)
    {
        switch (source)
        {
            case Source.LinkedIn or Source.LinkedInImport when IsNumericId(externalId):
                return await CheckPageAsync(new Uri($"https://www.linkedin.com/jobs/view/{externalId}/"), Site.LinkedIn, cancellationToken);

            case Source.KariyerNet when IsNumericId(externalId):
                // Any slug with the right id is answered with a redirect to the canonical one.
                return await CheckPageAsync(new Uri($"https://www.kariyer.net/is-ilani/ilan-{externalId}"), Site.KariyerNet, cancellationToken);

            case Source.Greenhouse or Source.Lever or Source.Ashby or Source.Workday or Source.Workable or Source.SmartRecruiters
                when url is not null:
                return await CheckAtsAsync(source, url, externalId, cancellationToken);

            default:
                return PostingLiveness.Unknown;
        }
    }

    private enum Site
    {
        LinkedIn,
        KariyerNet
    }

    private async Task<PostingLiveness> CheckPageAsync(Uri start, Site site, CancellationToken cancellationToken)
    {
        try
        {
            var current = start;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var status = (int)response.StatusCode;

                if (status is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);

                    if (site == Site.LinkedIn && PostingLivenessRules.IsLinkedInExpiredRedirect(next))
                    {
                        return PostingLiveness.Closed();
                    }

                    if (site == Site.KariyerNet && PostingLivenessRules.IsKariyerNetRemovedRedirect(next))
                    {
                        return PostingLiveness.Gone;
                    }

                    if (!IsSameSite(next, site))
                    {
                        return PostingLiveness.Unknown;
                    }

                    if (IsLoginWall(next))
                    {
                        return PostingLiveness.Stop;
                    }

                    current = next;
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var html = await ReadCappedAsync(response, cancellationToken);
                    return site == Site.LinkedIn
                        ? PostingLivenessRules.ReadLinkedInPage(html)
                        : PostingLivenessRules.ReadKariyerNetPage(html, _timeProvider.GetUtcNow());
                }

                return response.StatusCode switch
                {
                    HttpStatusCode.NotFound or HttpStatusCode.Gone => PostingLiveness.Gone,
                    HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden or LinkedInRequestDenied => PostingLiveness.Stop,
                    _ => PostingLiveness.Unknown
                };
            }

            return PostingLiveness.Unknown;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TimeoutRejectedException or BrokenCircuitException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return PostingLiveness.Unknown;
        }
    }

    /// <summary>
    /// The ATS APIs answer a removed posting with 404 ("Job not found" on Greenhouse, "Document not
    /// found" on Lever — measured 2026-09-27), which is as explicit as it gets. The other four were
    /// not measured, so for them a 404 is only a "gone" and needs its second look.
    /// </summary>
    private async Task<PostingLiveness> CheckAtsAsync(Source source, string url, string externalId, CancellationToken cancellationToken)
    {
        var result = await atsJobClient.GetPostingAsync(source, url, externalId, cancellationToken);
        return result.Outcome switch
        {
            JobSourceFetchOutcome.Ok => PostingLiveness.Open(),
            JobSourceFetchOutcome.RateLimited => PostingLiveness.Stop,
            JobSourceFetchOutcome.Error when result.StatusCode is 404 or 410 =>
                source is Source.Greenhouse or Source.Lever ? PostingLiveness.Closed() : PostingLiveness.Gone,
            _ => PostingLiveness.Unknown
        };
    }

    private static bool IsNumericId(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit);

    private static bool IsSameSite(Uri uri, Site site) =>
        HostRules.IsHttpsHost(uri, site == Site.LinkedIn ? "linkedin.com" : "kariyer.net");

    private static bool IsLoginWall(Uri uri) =>
        uri.AbsolutePath.StartsWith("/authwall", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/uas/login", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/checkpoint", StringComparison.OrdinalIgnoreCase);

    private async Task<string> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buffer = new char[options.Value.MaxBodyChars];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await reader.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return new string(buffer, 0, total);
    }
}
