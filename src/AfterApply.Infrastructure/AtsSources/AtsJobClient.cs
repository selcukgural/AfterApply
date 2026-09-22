using System.Net;
using System.Net.Http.Headers;
using AfterApply.Application.AtsSources;
using AfterApply.Application.Common;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.AtsSources;

/// <summary>
/// Fetches one posting from an ATS's public job-board API. Reuses <see cref="JobSourceHttpClient"/>
/// wholesale — same honest User-Agent, same hand-followed redirects with the allow-list re-checked
/// on every hop, same capped body read, same "every response becomes an outcome" contract — because
/// the rules that make the sweep well-behaved are not LinkedIn-specific.
///
/// Two differences from the sweep's clients. There is no login wall to detect: these endpoints are
/// public by design, so a redirect off the allow-list is the only "not for you" shape there is.
/// And the allow-list is <see cref="AtsApiUrlBuilder.ApiDomains"/>, the API hosts, not the page
/// hosts — a redirect from <c>boards-api.greenhouse.io</c> to a customer's own careers site is a
/// refusal, not a hop to follow.
///
/// The User-Agent is deliberately the same <c>JobSourceOptions.UserAgent</c> the sweep uses: it
/// identifies us to the site, and there is one of us.
/// </summary>
public sealed class AtsJobClient(HttpClient httpClient, IOptions<JobSourceOptions> options)
    : JobSourceHttpClient(httpClient, options), IAtsJobClient
{
    public Task<JobSourceFetchResult<AtsJobPosting>> GetPostingAsync(Source source, string jobUrl, string externalId,
        CancellationToken cancellationToken)
    {
        var uri = AtsApiUrlBuilder.Build(source, jobUrl, externalId);
        if (uri is null)
        {
            // No address we are willing to request: an id whose shape the builder could not prove,
            // or a source with no public endpoint. Not an error worth retrying.
            return Task.FromResult(new JobSourceFetchResult<AtsJobPosting>(
                JobSourceFetchOutcome.Blocked, StatusCode: null, DurationMs: 0, Value: null));
        }

        return FetchAsync(uri, (json, _) => AtsJobPostingParser.Parse(source, json, externalId)!, cancellationToken);
    }

    protected override bool IsAllowedHost(Uri uri) => HostRules.IsHttpsHost(uri, AtsApiUrlBuilder.ApiDomains);

    /// <summary>These endpoints are public; there is no login wall to be redirected into.</summary>
    protected override bool IsWall(Uri uri) => false;

    protected override bool IsDenied(HttpStatusCode status) => status is HttpStatusCode.Forbidden;

    protected override void ConfigureRequest(HttpRequestMessage request) =>
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    /// <summary>Ashby hands back a company's whole board rather than one posting, and a large
    /// board runs well past the HTML default.</summary>
    protected override int MaxBodyChars => 1_000_000;
}
