using AfterApply.Application.FeatureFlags;
using AfterApply.Application.JobLiveness;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobLiveness;

/// <summary>
/// Once a day, looks again at the postings someone is still waiting on — an application still in
/// progress, or a posting saved to apply to later — and records the ones the site now says are
/// closed. Each posting at most every few days, a capped number per run, one request at a time
/// with a pause, and a site that answers 429/403 or a login wall is left alone for the rest of
/// the run.
/// </summary>
/// <remarks>
/// Only sites with a measured closed signal are asked (see <see cref="PostingLivenessRules"/> and
/// <see cref="JobLivenessClient"/>); a posting from anywhere else has no id to look up and is never
/// selected. Logs carry counts and outcomes only — never a URL or anything about a person.
/// </remarks>
internal sealed class JobLivenessService(
    AppDbContext dbContext,
    IJobLivenessClient client,
    IFeatureFlags featureFlags,
    IOptions<JobLivenessOptions> options,
    ILogger<JobLivenessService> logger,
    TimeProvider? timeProvider = null) : IJobLivenessService
{
    private static readonly HashSet<Source> CheckedSources =
    [
        Source.LinkedIn, Source.LinkedInImport, Source.KariyerNet,
        Source.Greenhouse, Source.Lever, Source.Ashby, Source.Workday, Source.Workable, Source.SmartRecruiters
    ];

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.JobLiveness))
        {
            return;
        }

        var settings = options.Value;
        var now = _timeProvider.GetUtcNow();
        var dueBefore = now.AddDays(-settings.CheckIntervalDays);
        var confirmAfter = TimeSpan.FromHours(settings.ConfirmAfterHours);
        var suspectDueBefore = now - confirmAfter;
        var appliedSince = now.AddDays(-settings.OpenWindowDays);
        var terminal = TerminalApplicationStatuses.Values;

        var due = await dbContext.Jobs
            .Where(j => j.ClosedAt == null && j.ExternalId != null && CheckedSources.Contains(j.Source))
            .Where(j => j.LivenessCheckedAt == null
                        || j.LivenessCheckedAt < dueBefore
                        || (j.LivenessSuspectedAt != null && j.LivenessCheckedAt < suspectDueBefore))
            .Where(j => dbContext.Applications.Any(a => a.JobId == j.Id && !terminal.Contains(a.Status) && a.AppliedAt >= appliedSince)
                        || dbContext.TrackedJobs.Any(t => t.JobId == j.Id))
            .OrderBy(j => j.LivenessCheckedAt != null)
            .ThenBy(j => j.LivenessCheckedAt)
            .Take(settings.MaxChecksPerRun)
            .ToListAsync(cancellationToken);

        var stoppedSites = new HashSet<string>();
        var counts = new Dictionary<PostingLivenessKind, int>();
        var closed = 0;
        var requests = 0;

        foreach (var job in due)
        {
            var site = SiteOf(job.Source);
            if (stoppedSites.Contains(site))
            {
                continue;
            }

            if (requests++ > 0)
            {
                await PauseAsync(cancellationToken);
            }

            var observation = await client.CheckAsync(job.Source, job.ExternalId!, job.Url, cancellationToken);
            if (observation.SourceSaidStop)
            {
                stoppedSites.Add(site);
                logger.LogWarning("Job liveness: {Site} asked us to stop; the rest of its postings wait for the next run", site);
                continue;
            }

            counts[observation.Kind] = counts.GetValueOrDefault(observation.Kind) + 1;
            if (job.ApplyLiveness(observation, _timeProvider.GetUtcNow(), confirmAfter))
            {
                closed++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Job liveness: {Due} due, {Requests} checked, {Open} open, {Closed} closed now, {Gone} gone (unconfirmed), {Unknown} unknown, stopped sites {Stopped}",
            due.Count, requests, counts.GetValueOrDefault(PostingLivenessKind.Open), closed,
            counts.GetValueOrDefault(PostingLivenessKind.Gone), counts.GetValueOrDefault(PostingLivenessKind.Unknown),
            stoppedSites.Count);
    }

    /// <summary>LinkedIn postings reach us under two sources (captured and imported) but are one
    /// site to be polite to; each ATS is its own.</summary>
    private static string SiteOf(Source source) => source is Source.LinkedInImport ? nameof(Source.LinkedIn) : source.ToString();

    private async Task PauseAsync(CancellationToken cancellationToken)
    {
        var min = options.Value.MinDelayMs;
        if (min > 0)
        {
            await Task.Delay(min + Random.Shared.Next(min + 1), cancellationToken);
        }
    }
}
