using AfterApply.Application.FeatureFlags;
using System.Net;
using AfterApply.Application.Common;
using AfterApply.Application.Companies;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace AfterApply.Infrastructure.Companies;

/// <summary>
/// Finds a company's logo and keeps a copy, for the applications board. Two sources, in order
/// (DECISIONS.md 2026-09-28, "Şirket logosu: önce şirketin sitesi"):
/// <list type="number">
/// <item>the company's own website — the icon its home page declares (<see cref="WebsiteIconParser"/>);</item>
/// <item>its LinkedIn page's og:image, when the website gave nothing.</item>
/// </list>
///
/// Every URL involved is untrusted: the website and the LinkedIn URL came from one user's capture
/// (the website read off the company's LinkedIn or kariyer.net page), and the image URLs from those
/// pages. So: everything over https, every redirect hop re-checked; LinkedIn only from linkedin.com
/// and media.licdn.com, and the page must name this company (<see cref="CompanyPageIdentity"/>);
/// a website on any host, but only through <see cref="Http.PublicAddressGuard"/>, which refuses to
/// connect anywhere but the public internet; images capped at <see cref="CompanyLogoImage.MaxBytes"/>
/// while read, typed from their bytes (PNG, JPEG, WebP — never SVG), and a website's icon checked
/// for a logo's size, not a favicon's. The logo is served back from our own origin, so the viewer's
/// browser never asks a third party which companies they applied to.
///
/// Best-effort like <see cref="CompanyEnrichmentService"/>: "no logo there" is looked at again after
/// a month; "no answer" (LinkedIn's bot wall, a timeout, a 5xx) after 1, 2, 4… days
/// (<see cref="CompanyLogo.Deferred"/>).
/// </summary>
internal sealed class CompanyLogoService(
    HttpClient httpClient,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext,
    IBackgroundJobClient jobClient,
    IFeatureFlags featureFlags,
    ILogger<CompanyLogoService> logger,
    TimeProvider? timeProvider = null) : ICompanyLogoService
{
    /// <summary>The client for company websites: any host, through the public-address guard.</summary>
    public const string WebsiteClientName = "company-website-icons";

    private const int MaxRedirectHops = 5;
    private const int MaxLinkedInPageBytes = 200_000;
    private const int MaxWebsitePageBytes = 300_000;
    private const string LinkedInHost = "linkedin.com";

    /// <summary>A website icon smaller than this is a favicon, not a logo; larger than
    /// <see cref="MaxWebsiteIconSide"/> is not an icon at all.</summary>
    internal const int MinWebsiteIconSide = 64;

    internal const int MaxWebsiteIconSide = 2048;

    /// <summary>Companies scheduled per nightly run, one every <see cref="BackfillSpacing"/>.</summary>
    internal const int BackfillBatchSize = 50;

    internal static readonly TimeSpan BackfillSpacing = TimeSpan.FromSeconds(20);

    private static readonly DecoderOptions IdentifyOptions = new()
    {
        Configuration = new Configuration(new PngConfigurationModule(), new JpegConfigurationModule(), new WebpConfigurationModule())
    };

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task FetchAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.Board))
        {
            return;
        }

        var company = await dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Name, c.LinkedInUrl, c.Website })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return;
        }

        var websiteHome = WebsiteIconParser.HomePage(company.Website);
        var hasLinkedIn = TryAllowed(company.LinkedInUrl, LinkedInHost, out var pageUri);
        if (websiteHome is null && !hasLinkedIn)
        {
            // Nowhere to look yet: nothing is recorded, so a later capture that brings a website
            // or a LinkedIn page is picked up by the backfill.
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var logo = await dbContext.CompanyLogos.FirstOrDefaultAsync(l => l.CompanyId == companyId, cancellationToken);
        if (logo is not null && !logo.IsDue(now))
        {
            return;
        }

        CompanyLogoFile? found = null;
        var noAnswer = new List<string>();

        if (websiteHome is not null)
        {
            try
            {
                found = await FindOnWebsiteAsync(websiteHome, cancellationToken);
            }
            catch (TransientFetchException ex)
            {
                noAnswer.Add($"website {ex.Message}");
            }
        }

        if (found is null && hasLinkedIn)
        {
            try
            {
                found = await FindOnLinkedInAsync(company.Name, pageUri, companyId, cancellationToken);
            }
            catch (TransientFetchException ex)
            {
                noAnswer.Add($"LinkedIn {ex.Message}");
            }
        }

        if (logo is null)
        {
            logo = CompanyLogo.For(companyId);
            dbContext.CompanyLogos.Add(logo);
        }

        if (found is { } image)
        {
            logo.Found(image.Content, image.ContentType, now);
        }
        else if (noAnswer.Count > 0)
        {
            // A source that did not answer says nothing about the company; it is asked again soon,
            // and sooner than a month, but not every night.
            logo.Deferred(now);
            // The reason alone: a throttled fetch is expected, and a stack trace per company buried
            // the nightly logs.
            logger.LogInformation("Logo fetch for {CompanyId} deferred ({DeferCount}): {Reason}",
                companyId, logo.DeferCount, string.Join("; ", noAnswer));
        }
        else
        {
            logo.NotFound(now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Two jobs for one company (two captures in the same minute): the other one stored it.
            logger.LogInformation("Logo for {CompanyId} was stored by a concurrent job", companyId);
        }
    }

    public async Task<int> ScheduleBackfillAsync(CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.Board))
        {
            return 0;
        }

        var now = _timeProvider.GetUtcNow();

        // Only companies someone applied to or saved a posting at — the board is the only place a
        // logo is shown, so a company nobody tracks is not worth a request.
        var companyIds = await dbContext.Companies
            .Where(c => (c.Website != null || c.LinkedInUrl != null)
                && (dbContext.Applications.Any(a => a.CompanyId == c.Id) || dbContext.TrackedJobs.Any(t => t.CompanyId == c.Id))
                && !dbContext.CompanyLogos.Any(l => l.CompanyId == c.Id
                    && (l.Blocked || l.Content != null || (l.NextCheckAt != null && l.NextCheckAt > now))))
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(BackfillBatchSize)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < companyIds.Count; i++)
        {
            var companyId = companyIds[i];
            jobClient.Schedule<ICompanyLogoService>(s => s.FetchAsync(companyId, CancellationToken.None), BackfillSpacing * i);
        }

        return companyIds.Count;
    }

    public async Task<CompanyLogoFile?> GetForUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.Board))
        {
            return null;
        }

        var related = await dbContext.Applications.AnyAsync(a => a.UserId == userId && a.CompanyId == companyId, cancellationToken)
            || await dbContext.TrackedJobs.AnyAsync(t => t.UserId == userId && t.CompanyId == companyId, cancellationToken);
        if (!related)
        {
            return null;
        }

        var logo = await dbContext.CompanyLogos
            .Where(l => l.CompanyId == companyId && l.Content != null && !l.Blocked)
            .Select(l => new { l.Content, l.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        return logo is { Content: not null, ContentType: not null } ? new CompanyLogoFile(logo.Content, logo.ContentType) : null;
    }

    public async Task<bool> BlockAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return false;
        }

        var logo = await dbContext.CompanyLogos.FirstOrDefaultAsync(l => l.CompanyId == companyId, cancellationToken);
        if (logo is null)
        {
            logo = CompanyLogo.For(companyId);
            dbContext.CompanyLogos.Add(logo);
        }

        logo.Block(_timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- the company's own website -------------------------------------------------------------

    private async Task<CompanyLogoFile?> FindOnWebsiteAsync(Uri home, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WebsiteClientName);
        var page = await GetAsync(client, home, IsPublicHttpsName, MaxWebsitePageBytes, truncate: true, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var html = System.Text.Encoding.UTF8.GetString(page.Value.Body);
        foreach (var candidate in WebsiteIconParser.Candidates(html, page.Value.Url))
        {
            var iconUri = candidate.Scheme == Uri.UriSchemeHttps ? candidate : new UriBuilder(candidate) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
            byte[]? bytes;
            try
            {
                bytes = (await GetAsync(client, iconUri, IsPublicHttpsName, CompanyLogoImage.MaxBytes, truncate: false, cancellationToken))?.Body;
            }
            catch (TransientFetchException)
            {
                // The page answered; one of its icons not answering is not worth waiting a day for.
                continue;
            }

            if (bytes is null || CompanyLogoImage.DetectContentType(bytes) is not { } contentType
                || !await IsLogoSizedAsync(bytes, cancellationToken))
            {
                continue;
            }

            return new CompanyLogoFile(bytes, contentType);
        }

        return null;
    }

    private static async Task<bool> IsLogoSizedAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(bytes, writable: false);
            var info = await Image.IdentifyAsync(IdentifyOptions, stream, cancellationToken);
            return info.Width >= MinWebsiteIconSide && info.Height >= MinWebsiteIconSide
                && info.Width <= MaxWebsiteIconSide && info.Height <= MaxWebsiteIconSide;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or ImageFormatException or InvalidImageContentException)
        {
            return false;
        }
    }

    private static bool IsPublicHttpsName(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.HostNameType == UriHostNameType.Dns;

    // ---- LinkedIn ------------------------------------------------------------------------------

    private async Task<CompanyLogoFile?> FindOnLinkedInAsync(string companyName, Uri pageUri, Guid companyId,
        CancellationToken cancellationToken)
    {
        // The page is cut at the cap rather than refused: a company page runs to ~350 KB, and the
        // og:image and og:title it is read for sit in the first few kilobytes.
        var page = await GetAsync(httpClient, pageUri, uri => HostRules.IsHttpsHost(uri, LinkedInHost), MaxLinkedInPageBytes,
            truncate: true, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var html = System.Text.Encoding.UTF8.GetString(page.Value.Body);
        if (!CompanyPageIdentity.Matches(companyName, LinkedInCompanyProfileParser.ExtractCompanyName(html)))
        {
            logger.LogInformation("LinkedIn page of company {CompanyId} names another company; no logo taken", companyId);
            return null;
        }

        if (!TryAllowed(LinkedInCompanyProfileParser.ExtractLogoUrl(html), LinkedInCompanyProfileParser.LogoHost, out var imageUri))
        {
            return null;
        }

        var bytes = (await GetAsync(httpClient, imageUri, uri => HostRules.IsHttpsHost(uri, LinkedInCompanyProfileParser.LogoHost),
            CompanyLogoImage.MaxBytes, truncate: false, cancellationToken))?.Body;
        if (bytes is null)
        {
            return null;
        }

        var contentType = CompanyLogoImage.DetectContentType(bytes);
        return contentType is null ? null : new CompanyLogoFile(bytes, contentType);
    }

    // ---- fetching ------------------------------------------------------------------------------

    /// <summary>
    /// GET with manual redirects (each hop re-checked with <paramref name="allowed"/>) and a hard
    /// ceiling on the body: reading stops at <paramref name="maxBytes"/> + 1. A body past it is cut
    /// there when <paramref name="truncate"/> (a page), refused otherwise (an image, where a partial
    /// file is no file). Null for anything but a clean 200; the URL that answered comes back with
    /// the body, for resolving the page's relative links.
    /// </summary>
    private static async Task<(byte[] Body, Uri Url)?> GetAsync(HttpClient client, Uri uri, Func<Uri, bool> allowed, int maxBytes,
        bool truncate, CancellationToken cancellationToken)
    {
        if (!allowed(uri))
        {
            return null;
        }

        try
        {
            var current = uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd("EKariyerimLinkPreview/1.0 (+https://ekariyerim.com)");

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!allowed(next))
                    {
                        return null;
                    }

                    current = next;
                    continue;
                }

                if (IsTransient(response.StatusCode))
                {
                    throw new TransientFetchException($"HTTP {(int)response.StatusCode}");
                }

                if (response.StatusCode != HttpStatusCode.OK || (!truncate && response.Content.Headers.ContentLength > maxBytes))
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[maxBytes + 1];
                var total = 0;
                int read;
                while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
                {
                    total += read;
                }

                if (total > maxBytes)
                {
                    return truncate ? (buffer[..maxBytes], current) : null;
                }

                return (buffer[..total], current);
            }

            return null;
        }
        catch (HttpRequestException ex) when (Http.PublicAddressGuard.IsRefusal(ex))
        {
            // A website that points at a private or internal address has no logo for us, now or later.
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new TransientFetchException(ex is TaskCanceledException ? "timeout" : ex.GetType().Name, ex);
        }
    }

    /// <summary>Answers that say "not now" rather than "not here": LinkedIn's bot wall (999), rate
    /// limiting and server errors.</summary>
    private static bool IsTransient(HttpStatusCode status) =>
        (int)status is 999 or 429 || (int)status >= 500;

    private sealed class TransientFetchException(string reason, Exception? inner = null) : Exception(reason, inner);

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool TryAllowed(string? url, string host, out Uri uri)
    {
        uri = null!;
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !HostRules.IsHttpsHost(parsed, host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
