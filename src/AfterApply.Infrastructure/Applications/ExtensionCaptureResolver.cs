using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.AtsSources;
using AfterApply.Application.Companies;
using AfterApply.Application.Imports;
using AfterApply.Domain.Common;
using Hangfire;

namespace AfterApply.Infrastructure.Applications;

/// <summary>
/// Turns what the browser extension read off a posting page into the shared Company and Job rows
/// it belongs to, and queues the enrichment those rows are due. One place for it because two
/// buttons submit the same capture: "I Applied" (an Application) and "Apply later" (a TrackedJob),
/// and a posting saved first and applied to later must land on the same Company and Job either way.
/// </summary>
internal sealed class ExtensionCaptureResolver(
    ICompanyResolver companyResolver, IJobResolver jobResolver, ICompanySearchService companySearchService,
    IBackgroundJobClient jobClient)
{
    public async Task<(Guid CompanyId, Guid JobId)> ResolveAsync(Guid userId, CreateFromExtensionRequest request,
        string normalizedUrl, CancellationToken cancellationToken)
    {
        // Scraped names are often near-duplicates of an existing Company (typos, "Corp" vs
        // "Corporation") rather than a genuinely new one — a high-confidence trigram match is
        // silently attached to first, falling back to the unchanged exact-match-or-create
        // resolver only when no such match exists. Manual entry is unaffected: it still calls
        // ResolveOrCreateAsync directly, since the autocomplete UI already steers users to type an
        // existing company's exact name when one applies.
        //
        // CompanyAtsUrl arrives as a URL, not a platform name — the platform is whatever the
        // resolver says that host is, so a client cannot mislabel a Greenhouse board as a Workday
        // one. IsAts filters out the case where a validator-allowed host somehow resolves to
        // something else, rather than storing a link under a platform it does not belong to.
        var (atsPlatform, _) = request.CompanyAtsUrl is null
            ? (Source.Other, null)
            : JobPostingSourceResolver.Resolve(request.CompanyAtsUrl);
        var profileLinks = new CompanyProfileLinks(
            request.CompanyLinkedInUrl,
            request.CompanyKariyerNetUrl,
            JobPostingSourceResolver.IsAts(atsPlatform) ? request.CompanyAtsUrl : null,
            JobPostingSourceResolver.IsAts(atsPlatform) ? atsPlatform : null,
            SubmittedBy: userId);

        var companyId = await companySearchService.FindHighConfidenceMatchAsync(request.CompanyName, cancellationToken)
            ?? await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken, profileLinks);
        await companyResolver.RecordProfileSubmissionsAsync(companyId, profileLinks, cancellationToken);

        // Only worth queuing when this submission actually carries a profile URL — a company
        // matched via the trigram/high-confidence path above, or one whose posting linked to
        // neither profile, has nothing new for CompanyEnrichmentService to fetch from. Safe to
        // enqueue immediately: by this point the Company row is already committed, either from an
        // earlier request or by CompanyResolver's own SaveChangesAsync just above — the enqueued
        // job only touches Company, never the caller's own not-yet-saved row.
        if (profileLinks.HasAny)
        {
            jobClient.Enqueue<ICompanyEnrichmentService>(s => s.EnrichAsync(companyId, CancellationToken.None));
        }

        // The job posting's own site (LinkedIn, kariyer.net, ...) tags Job.Source — data
        // provenance — while the caller's row carries how it was created.
        var (jobSource, externalId) = JobPostingSourceResolver.Resolve(normalizedUrl);
        // No description onto the shared Job: it is this user's capture, and it goes on their own
        // row (see IJobResolver).
        var jobId = await jobResolver.ResolveOrCreateAsync(companyId, request.JobTitle, jobSource, normalizedUrl,
            externalId, request.Location, cancellationToken, request.PublishedAt);

        // An ATS posting can be read back from that ATS's own public API, which is worth doing
        // when the page scrape came back without a usable description — the field CV scanning and
        // job-fit scoring both need. The service re-checks the flag, the length and the source
        // itself, so this is only a cheap "might be worth a look", and it runs after the Job row
        // is committed by the resolver above.
        if (JobPostingSourceResolver.IsAts(jobSource) && externalId is not null)
        {
            jobClient.Enqueue<IAtsJobEnrichmentService>(s => s.EnrichAsync(jobId, CancellationToken.None));
        }

        return (companyId, jobId);
    }
}
