using System.Net;
using AfterApply.Application.Imports;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.TrackedJobs;

/// <summary>
/// Resolves a company/job-title preview for a pasted job URL — built for the mobile client,
/// which (unlike the browser extension) has no page DOM to scrape, only the URL the user copied.
/// Deliberately never runs a real browser or executes JavaScript: LinkedIn shows a login-wall
/// overlay to logged-out visitors, but that overlay is client-side chrome layered on top of
/// server-rendered HTML by LinkedIn's own JS — a plain HTTP fetch never renders or sees it, and
/// (confirmed empirically) the underlying HTML still carries real metadata regardless. The short
/// "/jobs/view/{id}/" link a user copies always resolves to a canonical, slug-based URL that
/// encodes the title and company (LinkedInJobSlugParser) — whether LinkedIn hands that back as a
/// 301 redirect's Location header (no body download needed) or serves the page directly at 200
/// with a <c>&lt;link rel="canonical"&gt;</c> pointing at it (which response we get for the same
/// URL varies with User-Agent/locale, so both paths are handled). kariyer.net (confirmed
/// empirically too — same honest bot User-Agent, no CAPTCHA hit) publishes a meta description
/// with its own unambiguous company/title template (KariyerNetJobDescriptionParser); any other
/// allow-listed host, or a host-specific parse miss, falls back to a generic
/// og:title/&lt;title&gt; read (company left null — nothing to split it from).
///
/// Only linkedin.com/kariyer.net (and subdomains) are ever fetched, and every redirect hop is
/// re-checked against that allow-list before being followed — this fetches a URL supplied by an
/// end user, so anything else is refused outright (SSRF).
/// </summary>
internal sealed class JobLinkPreviewService(HttpClient httpClient, ILogger<JobLinkPreviewService> logger) : IJobLinkPreviewService
{
    private const int MaxRedirectHops = 5;
    private const int MaxBodyChars = 65536;
    private static readonly string[] AllowedHosts = ["linkedin.com", "kariyer.net"];

    public async Task<TrackedJobLinkPreviewResponse> ResolveAsync(string jobUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri) || !IsAllowed(uri))
        {
            return Empty(jobUrl);
        }

        try
        {
            var currentUri = uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.UserAgent.ParseAdd("AfterApplyLinkPreview/1.0 (+https://afterapply.app)");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var location = response.Headers.Location;

                if (IsRedirect(response.StatusCode) && location is not null)
                {
                    var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    if (!IsAllowed(nextUri))
                    {
                        return Empty(jobUrl);
                    }

                    if (IsHost(nextUri, "linkedin.com"))
                    {
                        var (title, company) = LinkedInJobSlugParser.Parse(nextUri.ToString());
                        if (title is not null && company is not null)
                        {
                            return new TrackedJobLinkPreviewResponse(company, title, jobUrl);
                        }
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var html = await ReadCappedAsync(response, cancellationToken);

                    // LinkedIn always carries <link rel="canonical"> pointing at the slug URL
                    // (title-at-company-id), even on a request that got served at 200 with no
                    // redirect (redirect-vs-200 behavior varies with User-Agent/locale) — reading
                    // it from the body gets the same clean split as the redirect-header path.
                    if (IsHost(currentUri, "linkedin.com"))
                    {
                        var canonicalHref = OpenGraphMetadataParser.ExtractLinkHref(html, "canonical");
                        var (canonicalTitle, canonicalCompany) = LinkedInJobSlugParser.Parse(canonicalHref);
                        if (canonicalTitle is not null && canonicalCompany is not null)
                        {
                            return new TrackedJobLinkPreviewResponse(canonicalCompany, canonicalTitle, jobUrl);
                        }
                    }

                    // kariyer.net's <title>/og:title concatenate "{Company} {JobTitle}" with no
                    // delimiter (can't be split reliably), but its meta description follows a
                    // fixed template with an unambiguous one ("... {Company} firmasına ait
                    // {JobTitle} ...") — see KariyerNetJobDescriptionParser.
                    if (IsHost(currentUri, "kariyer.net"))
                    {
                        var description = OpenGraphMetadataParser.ExtractProperty(html, "og:description")
                            ?? OpenGraphMetadataParser.ExtractProperty(html, "description");
                        var (kariyerTitle, kariyerCompany) = KariyerNetJobDescriptionParser.Parse(description);
                        if (kariyerTitle is not null && kariyerCompany is not null)
                        {
                            return new TrackedJobLinkPreviewResponse(kariyerCompany, kariyerTitle, jobUrl);
                        }
                    }

                    var title = OpenGraphMetadataParser.ExtractProperty(html, "og:title")
                        ?? OpenGraphMetadataParser.ExtractTitleTag(html);
                    return new TrackedJobLinkPreviewResponse(null, title, jobUrl);
                }

                return Empty(jobUrl);
            }

            return Empty(jobUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogInformation(ex, "Job link preview fetch failed for {JobUrl}", jobUrl);
            return Empty(jobUrl);
        }
    }

    private static TrackedJobLinkPreviewResponse Empty(string jobUrl) => new(null, null, jobUrl);

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

    private static bool IsAllowed(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && AllowedHosts.Any(host => IsHost(uri, host));

    private static bool IsHost(Uri uri, string domain) =>
        uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
