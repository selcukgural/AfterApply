using System.Net;
using AfterApply.Application.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.Companies;

/// <summary>
/// Background enrichment, queued via Hangfire right after a Company row is resolved from the
/// browser extension's "create application" flow (see ApplicationService.CreateFromExtensionAsync
/// and CompanyResolver's backfill path): once a Company has a LinkedInUrl, fetch that LinkedIn
/// company page — plain HTTP, honest bot User-Agent, same technique already validated for job
/// postings in JobLinkPreviewService — and fill in Website/Industry/Country from its public
/// "About" overview.
///
/// Best-effort only: no LinkedInUrl yet, already fully enriched, a disallowed/redirected host, a
/// network error, or LinkedIn markup that no longer matches the parser all just leave the fields
/// as they were — same graceful-degradation philosophy as the rest of the extension-import
/// pipeline (a failed enrichment never blocks or fails the application that triggered it, since it
/// always runs after that row is already saved).
///
/// Company.LinkedInUrl is client-supplied (from the extension's DOM scrape) and stored, so it is
/// re-validated against the linkedin.com allow-list here — never trusted as already-safe just
/// because it made it into the database (defense in depth, same as JobLinkPreviewService's own
/// re-check of every redirect hop).
/// </summary>
internal sealed class CompanyEnrichmentService(
    HttpClient httpClient, AppDbContext dbContext, ILogger<CompanyEnrichmentService> logger) : ICompanyEnrichmentService
{
    private const int MaxRedirectHops = 5;
    private const int MaxBodyChars = 200_000;

    public async Task EnrichAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company?.LinkedInUrl is null)
        {
            return;
        }

        if (company.Website is not null && company.Industry is not null && company.Country is not null)
        {
            return;
        }

        if (!Uri.TryCreate(company.LinkedInUrl, UriKind.Absolute, out var uri) || !IsAllowed(uri))
        {
            return;
        }

        var html = await FetchAsync(uri, companyId, cancellationToken);
        if (html is null)
        {
            return;
        }

        var website = LinkedInCompanyProfileParser.ExtractWebsite(html);
        var industry = LinkedInCompanyProfileParser.ExtractIndustry(html);
        var country = LinkedInCompanyProfileParser.ExtractCountryCode(html);

        company.EnrichFrom(website, industry, country, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> FetchAsync(Uri uri, Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            var currentUri = uri;
            for (var hop = 0; hop < MaxRedirectHops; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.UserAgent.ParseAdd("EKariyerimLinkPreview/1.0 (+https://ekariyerim.com)");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var location = response.Headers.Location;

                if (IsRedirect(response.StatusCode) && location is not null)
                {
                    var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    if (!IsAllowed(nextUri))
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

    private static bool IsAllowed(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase));
}
