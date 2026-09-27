using AfterApply.Application.FeatureFlags;
using System.Net;
using AfterApply.Application.Common;
using AfterApply.Application.Companies;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Board;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.Companies;

/// <summary>
/// Finds a company's logo on its LinkedIn page and keeps a copy, for the applications board.
///
/// Every URL involved is untrusted: the profile URL came from one user's extension capture, and the
/// image URL from that page. So: the page is fetched only from linkedin.com and the image only from
/// media.licdn.com, both over https, with every redirect hop re-checked against the same host; the
/// page must name this company (<see cref="CompanyPageIdentity"/>) or nothing on it is used; the
/// image is capped at <see cref="CompanyLogoImage.MaxBytes"/> while it is read, not after; and its
/// type is read from its bytes — PNG, JPEG or WebP only, never SVG.
///
/// Best-effort like <see cref="CompanyEnrichmentService"/>: a page that is gone, blocked or changed
/// records "not found" and is looked at again after <see cref="RetryAfter"/>.
/// </summary>
internal sealed class CompanyLogoService(
    HttpClient httpClient,
    AppDbContext dbContext,
    IBackgroundJobClient jobClient,
    IFeatureFlags featureFlags,
    ILogger<CompanyLogoService> logger,
    TimeProvider? timeProvider = null) : ICompanyLogoService
{
    private const int MaxRedirectHops = 5;
    private const int MaxPageBytes = 200_000;
    private const string LinkedInHost = "linkedin.com";

    /// <summary>How long a "no logo found" stands before the company is looked at again.</summary>
    internal static readonly TimeSpan RetryAfter = TimeSpan.FromDays(30);

    /// <summary>Companies scheduled per nightly run, one every <see cref="BackfillSpacing"/>.</summary>
    internal const int BackfillBatchSize = 50;

    internal static readonly TimeSpan BackfillSpacing = TimeSpan.FromSeconds(20);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task FetchAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.Board))
        {
            return;
        }

        var company = await dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Name, c.LinkedInUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null || !TryAllowed(company.LinkedInUrl, LinkedInHost, out var pageUri))
        {
            // No LinkedIn page yet: nothing is recorded, so a later capture that brings one is
            // picked up by the backfill.
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var logo = await dbContext.CompanyLogos.FirstOrDefaultAsync(l => l.CompanyId == companyId, cancellationToken);
        if (logo is { Blocked: true } || logo?.Content is not null || (logo is not null && logo.CheckedAt > now - RetryAfter))
        {
            return;
        }

        CompanyLogoFile? found;
        try
        {
            found = await FindLogoAsync(company.Name, pageUri, companyId, cancellationToken);
        }
        catch (TransientFetchException ex)
        {
            // LinkedIn throttling (its 999, or a 429), a 5xx or a dropped connection says nothing
            // about the company. Nothing is recorded, so the next nightly run asks again instead
            // of a month from now.
            logger.LogInformation(ex, "Logo fetch for {CompanyId} deferred: {Reason}", companyId, ex.Message);
            return;
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

        var retryBefore = _timeProvider.GetUtcNow() - RetryAfter;

        // Only companies someone applied to or saved a posting at — the board is the only place a
        // logo is shown, so a company nobody tracks is not worth a request to LinkedIn.
        var companyIds = await dbContext.Companies
            .Where(c => c.LinkedInUrl != null
                && (dbContext.Applications.Any(a => a.CompanyId == c.Id) || dbContext.TrackedJobs.Any(t => t.CompanyId == c.Id))
                && !dbContext.CompanyLogos.Any(l => l.CompanyId == c.Id
                    && (l.Blocked || l.Content != null || l.CheckedAt > retryBefore)))
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

    private async Task<CompanyLogoFile?> FindLogoAsync(string companyName, Uri pageUri, Guid companyId,
        CancellationToken cancellationToken)
    {
        // The page is cut at the cap rather than refused: a company page runs to ~350 KB, and the
        // og:image and og:title it is read for sit in the first few kilobytes.
        var page = await FetchAsync(pageUri, LinkedInHost, MaxPageBytes, truncate: true, companyId, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var html = System.Text.Encoding.UTF8.GetString(page);
        if (!CompanyPageIdentity.Matches(companyName, LinkedInCompanyProfileParser.ExtractCompanyName(html)))
        {
            logger.LogInformation("LinkedIn page of company {CompanyId} names another company; no logo taken", companyId);
            return null;
        }

        if (!TryAllowed(LinkedInCompanyProfileParser.ExtractLogoUrl(html), LinkedInCompanyProfileParser.LogoHost, out var imageUri))
        {
            return null;
        }

        var bytes = await FetchAsync(imageUri, LinkedInCompanyProfileParser.LogoHost, CompanyLogoImage.MaxBytes, truncate: false,
            companyId, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        var contentType = CompanyLogoImage.DetectContentType(bytes);
        return contentType is null ? null : new CompanyLogoFile(bytes, contentType);
    }

    /// <summary>
    /// GET with manual redirects (each hop re-checked against <paramref name="host"/>) and a hard
    /// ceiling on the body: reading stops at <paramref name="maxBytes"/> + 1. A body past it is
    /// cut there when <paramref name="truncate"/> (a page), refused otherwise (an image, where a
    /// partial file is no file). Null for anything but a clean 200.
    /// </summary>
    private async Task<byte[]?> FetchAsync(Uri uri, string host, int maxBytes, bool truncate, Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd("EKariyerimLinkPreview/1.0 (+https://ekariyerim.com)");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!HostRules.IsHttpsHost(next, host))
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
                    return truncate ? buffer[..maxBytes] : null;
                }

                return buffer[..total];
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new TransientFetchException(ex.GetType().Name, ex);
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
