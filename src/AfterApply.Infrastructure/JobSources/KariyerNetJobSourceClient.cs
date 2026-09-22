using System.Net;
using AfterApply.Application.Common;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// Fetches kariyer.net's server-rendered listing and posting pages the way a logged-out browser
/// would (see <see cref="JobSourceHttpClient"/> for the shared rules). Two things are this
/// site's own: the search is answered with a 301 that adds the site's city ids, followed like
/// any other same-host redirect; and an unknown city is answered with a redirect to the bare
/// nationwide listing — reported as an empty page, not as fifty postings from the wrong place.
/// A redirect onto <c>/aday/giris</c> (the login) is the wall.
/// </summary>
public sealed class KariyerNetJobSourceClient(HttpClient httpClient, IOptions<JobSourceOptions> options, TimeProvider? timeProvider = null)
    : JobSourceHttpClient(httpClient, options), IKariyerNetJobSourceClient
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Source Source => Source.KariyerNet;

    protected override int MaxBodyChars => 450_000;

    public Task<JobSourceFetchResult<JobSourceSearchPage>> SearchAsync(JobSourceQuery query, int page, CancellationToken cancellationToken)
    {
        var uri = KariyerNetJobSearchUrlBuilder.Search(query.Keywords, query.Location, page);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return FetchAsync(uri, (html, finalUri) =>
        {
            if (!KariyerNetJobSearchUrlBuilder.IsSearchFor(finalUri, query.Location))
            {
                return new JobSourceSearchPage([], HasMore: false);
            }

            var parsed = KariyerNetJobSearchCardParser.Parse(html, today, page);
            if (!query.RemoteOnly)
            {
                return parsed;
            }

            // No URL filter for the work model; the card says it, so the filter is applied here.
            var remote = parsed.Cards.Where(c => c.WorkModel?.Contains("Uzaktan", StringComparison.OrdinalIgnoreCase) == true).ToList();
            return parsed with { Cards = remote };
        }, cancellationToken);
    }

    public Task<JobSourceFetchResult<JobSourceDetail>> GetPostingAsync(string externalId, CancellationToken cancellationToken)
    {
        var uri = KariyerNetJobSearchUrlBuilder.Posting(externalId);
        return FetchAsync(uri, (html, _) => KariyerNetJobPostingParser.Parse(html), cancellationToken);
    }

    protected override bool IsAllowedHost(Uri uri) => HostRules.IsHttpsHost(uri, "kariyer.net");

    protected override bool IsWall(Uri uri) =>
        uri.AbsolutePath.StartsWith("/aday/giris", StringComparison.OrdinalIgnoreCase)
        || uri.AbsolutePath.StartsWith("/website/kariyerim/login", StringComparison.OrdinalIgnoreCase);

    protected override bool IsDenied(HttpStatusCode status) => status is HttpStatusCode.Forbidden;
}
