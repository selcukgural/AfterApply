using AfterApply.Application.AtsSources;
using AfterApply.Application.Imports;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AfterApply.Infrastructure.AtsSources;

/// <summary>
/// Reads an ATS-hosted posting back from that ATS's own public API and fills in what the page
/// scrape could not. Queued by Hangfire right after the application row is saved, the same way
/// CompanyEnrichmentService is.
///
/// Why it exists at all, given the extension already reads the page: the description is the field
/// that makes the rest of the product work. CV scanning and AI job-fit scoring have nothing to
/// score without it, and a scrape that missed it — a page that renders its body after load, a
/// layout the generic JSON-LD path could not reach, a capture made from a listing rather than the
/// posting — leaves that application permanently thinner than the ones next to it. These APIs are
/// public, unauthenticated and stable, so the field can simply be fetched.
///
/// Best-effort in exactly the sense the rest of the import pipeline is: it runs after the
/// application is already committed, every failure leaves the row as it was, and it never
/// overwrites something the scrape got right (Job.EnrichFrom is fill-if-missing).
/// </summary>
internal sealed class AtsJobEnrichmentService(
    IAtsJobClient client,
    AppDbContext dbContext,
    IDistributedLockProvider locks,
    DistributedLockNames lockNames,
    IOptions<AtsSourceOptions> options,
    ILogger<AtsJobEnrichmentService> logger) : IAtsJobEnrichmentService
{
    public async Task EnrichAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            // Checked here rather than only at enqueue time, so a job already on the queue when
            // the flag goes off does not fetch anyway. Mirrors JobSourceSweepService.
            return;
        }

        // Two users applying to the same posting at the same moment enqueue two of these. The
        // lock is held for the fetch; a worker that cannot take it leaves, because whoever holds
        // it is doing this exact work. Redis being unreachable degrades to running unlocked
        // rather than failing — a duplicate fetch is wasteful, not wrong.
        IDistributedSynchronizationHandle? held;
        try
        {
            held = await locks.TryAcquireLockAsync(lockNames.AtsJobEnrichment(jobId), TimeSpan.Zero, cancellationToken);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "ATS enrichment lock for {JobId} unavailable; proceeding without it", jobId);
            await EnrichUnlockedAsync(jobId, cancellationToken);
            return;
        }

        if (held is null)
        {
            logger.LogInformation("ATS enrichment for {JobId} is already running elsewhere; skipping", jobId);
            return;
        }

        await using (held)
        {
            await EnrichUnlockedAsync(jobId, cancellationToken);
        }
    }

    private async Task EnrichUnlockedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.Url is null || job.ExternalId is null || !JobPostingSourceResolver.IsAts(job.Source))
        {
            return;
        }

        // The scrape is the primary source. Only a missing or stub-length description is worth a
        // request — re-reading a posting we already have in full buys nothing and costs the ATS.
        if (job.Description is { Length: > 0 } description && description.Length >= options.Value.MinDescriptionChars)
        {
            return;
        }

        var result = await client.GetPostingAsync(job.Source, job.Url, job.ExternalId, cancellationToken);
        if (!result.IsOk)
        {
            logger.LogInformation("ATS enrichment for {JobId} ({Source}) returned {Outcome}",
                jobId, job.Source, result.Outcome);
            return;
        }

        var posting = result.Value!;
        var changed = job.EnrichFrom(posting.Description, posting.DescriptionHtml, posting.Location,
            posting.EmploymentType, posting.PublishedAt, DateTimeOffset.UtcNow);

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
