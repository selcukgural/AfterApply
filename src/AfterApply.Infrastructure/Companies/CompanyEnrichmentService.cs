using System.Net;
using AfterApply.Application.Common;
using AfterApply.Application.Companies;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AfterApply.Infrastructure.Companies;

/// <summary>
/// Background enrichment, queued via Hangfire right after a Company row is resolved from the
/// browser extension's "create application" flow (see ApplicationService.CreateFromExtensionAsync
/// and CompanyResolver's backfill path): once a Company has a profile URL, fetch that company page
/// — plain HTTP, honest bot User-Agent, same technique already validated for job postings in
/// JobLinkPreviewService — and fill in whatever of Website/Industry/Country it publishes.
///
/// Two sources, tried in that order:
///
/// <list type="bullet">
/// <item>LinkedIn's public "About" overview, which carries all three fields.</item>
/// <item>kariyer.net's /firma-profil/ page, which carries the website (on roughly 44% of profiles,
/// measured 2026-09-06) and the sector, but no country. It runs only for whatever LinkedIn did not
/// fill — usually everything, since a company first seen in a kariyer.net posting has no LinkedIn
/// URL at all, which is exactly the gap it exists to close.</item>
/// </list>
///
/// Best-effort only: no profile URL yet, already fully enriched, a disallowed/redirected host, a
/// network error, or markup that no longer matches the parser all just leave the fields as they
/// were — same graceful-degradation philosophy as the rest of the extension-import pipeline (a
/// failed enrichment never blocks or fails the application that triggered it, since it always runs
/// after that row is already saved).
///
/// Both stored URLs are client-supplied (from the extension's DOM scrape), so each is re-validated
/// against its own host allow-list here — never trusted as already-safe just because it made it
/// into the database (defense in depth, same as JobLinkPreviewService's own re-check of every
/// redirect hop).
/// </summary>
internal sealed class CompanyEnrichmentService(
    HttpClient httpClient,
    AppDbContext dbContext,
    ICompanyCacheInvalidator invalidator,
    IDistributedLockProvider locks,
    DistributedLockNames lockNames,
    ILogger<CompanyEnrichmentService> logger) : ICompanyEnrichmentService
{
    private const int MaxRedirectHops = 5;
    private const int MaxBodyChars = 200_000;
    private const string LinkedInHost = "linkedin.com";
    private const string KariyerNetHost = "kariyer.net";

    public async Task EnrichAsync(Guid companyId, CancellationToken cancellationToken)
    {
        // Two applications for a new company created at the same moment enqueue two of these,
        // and with several instances they run on two workers at once — two fetches of the same
        // profile page, last write wins. The lock is held for the fetch; a worker that cannot take
        // it leaves, because whoever holds it is doing exactly this work. Redis being unreachable
        // degrades to the unlocked behaviour of before rather than failing the job.
        IDistributedSynchronizationHandle? held;
        try
        {
            held = await locks.TryAcquireLockAsync(lockNames.CompanyEnrichment(companyId), TimeSpan.Zero, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Enrichment lock for {CompanyId} unavailable; proceeding without it", companyId);
            held = null;
            await EnrichUnlockedAsync(companyId, cancellationToken);
            return;
        }

        if (held is null)
        {
            logger.LogInformation("Enrichment for {CompanyId} is already running elsewhere; skipping", companyId);
            return;
        }

        await using (held)
        {
            await EnrichUnlockedAsync(companyId, cancellationToken);
        }
    }

    private async Task EnrichUnlockedAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null)
        {
            return;
        }

        if (company.Website is not null && company.Industry is not null && company.Country is not null)
        {
            return;
        }

        if (TryParseAllowed(company.LinkedInUrl, LinkedInHost, out var linkedInUri))
        {
            var html = await FetchAsync(linkedInUri, companyId, cancellationToken);
            if (html is not null)
            {
                company.EnrichFrom(
                    LinkedInCompanyProfileParser.ExtractWebsite(html),
                    LinkedInCompanyProfileParser.ExtractIndustry(html),
                    LinkedInCompanyProfileParser.ExtractCountryCode(html),
                    DateTimeOffset.UtcNow);
            }
        }

        // Only worth a second request when LinkedIn left something kariyer.net can actually
        // supply. It publishes no country at all, so asking for one back is not a reason to fetch.
        if ((company.Website is null || company.Industry is null)
            && TryParseAllowed(company.KariyerNetUrl, KariyerNetHost, out var kariyerNetUri))
        {
            var html = await FetchAsync(kariyerNetUri, companyId, cancellationToken);
            if (html is not null)
            {
                company.EnrichFrom(
                    KariyerNetCompanyProfileParser.ExtractWebsite(html),
                    KariyerNetCompanyProfileParser.ExtractSector(html),
                    country: null,
                    DateTimeOffset.UtcNow);
            }
        }

        // The public company page shows the website, so a fill-in is a write to that page.
        if (await dbContext.SaveChangesAsync(cancellationToken) > 0)
        {
            await invalidator.InvalidateCompanyAsync(companyId, cancellationToken);
        }
    }

    private async Task<string?> FetchAsync(AllowedUri allowed, Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            var currentUri = allowed.Uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.UserAgent.ParseAdd("EKariyerimLinkPreview/1.0 (+https://ekariyerim.com)");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var location = response.Headers.Location;

                if (IsRedirect(response.StatusCode) && location is not null)
                {
                    var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    if (!IsAllowed(nextUri, allowed.Host))
                    {
                        return null;
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await ReadCappedAsync(response, cancellationToken);
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogInformation(ex, "Company profile fetch failed for {CompanyId}", companyId);
            return null;
        }
    }

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

    // A stored URL only ever gets fetched against the one host it is supposed to belong to, so a
    // LinkedIn URL that somehow ended up in KariyerNetUrl (or anything else) is refused rather
    // than followed.
    private static bool TryParseAllowed(string? url, string host, out AllowedUri allowed)
    {
        allowed = default;
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsAllowed(uri, host))
        {
            return false;
        }

        allowed = new AllowedUri(uri, host);
        return true;
    }

    private static bool IsAllowed(Uri uri, string host) => HostRules.IsHttpsHost(uri, host);

    private readonly record struct AllowedUri(Uri Uri, string Host);
}
