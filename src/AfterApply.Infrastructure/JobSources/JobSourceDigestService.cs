using AfterApply.Application.JobSources;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

internal sealed class JobSourceDigestService(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IBackgroundJobClient jobClient,
    IOptions<AppOptions> appOptions,
    ILogger<JobSourceDigestService> logger,
    TimeProvider? timeProvider = null) : IJobSourceDigestService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> EnqueueWeekAsync(int weekKey, CancellationToken cancellationToken)
    {
        // Users with something to announce this week, who want the e-mail, and were not told yet.
        var userIds = await dbContext.UserJobSourceRuns
            .Where(r => r.WeekKey == weekKey && r.DeliveredCount > 0 && r.DigestSentAt == null)
            .Where(r => dbContext.UserJobSourceProfiles.Any(p => p.UserId == r.UserId && p.EmailDigestEnabled))
            .Select(r => r.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            jobClient.Enqueue<IJobSourceDigestService>(s => s.SendAsync(userId, weekKey, CancellationToken.None));
        }

        return userIds.Count;
    }

    public async Task SendAsync(Guid userId, int weekKey, CancellationToken cancellationToken)
    {
        var run = await dbContext.UserJobSourceRuns.SingleOrDefaultAsync(r => r.UserId == userId && r.WeekKey == weekKey, cancellationToken);
        if (run is null || run.DigestSentAt is not null)
        {
            return;
        }

        var user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken);
        var profile = await dbContext.UserJobSourceProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.MinScore, p.EmailDigestEnabled })
            .SingleOrDefaultAsync(cancellationToken);
        if (user?.Email is null || profile is null || !profile.EmailDigestEnabled)
        {
            return;
        }

        // The same rows the page shows: this week's deliveries above the user's threshold (an
        // unscored one is never hidden by it), best first.
        var shown = await dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.WeekKey == weekKey && (d.Score == null || d.Score >= profile.MinScore))
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => new { d.Score, p.Title, p.CompanyName })
            .OrderByDescending(x => x.Score.HasValue).ThenByDescending(x => x.Score)
            .ToListAsync(cancellationToken);
        if (shown.Count == 0)
        {
            // Nothing the user would see; saying "0 postings are ready" is noise. Stamped so the
            // week is not re-examined.
            run.MarkDigestSent(_timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var best = shown[0].Score is null ? null : shown[0];
        var locale = user.PreferredLanguage is "en" ? "en" : "tr";
        var digest = new WeeklyJobsDigest(shown.Count, best?.Title, best?.CompanyName, best?.Score,
            $"{appOptions.Value.WebBaseUrl}/{locale}/weekly-jobs");

        await emailSender.SendWeeklyJobsReadyEmailAsync(user.Email, locale, digest, cancellationToken);

        run.MarkDigestSent(_timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Weekly jobs digest sent for week {WeekKey}: {Count} postings", weekKey, shown.Count);
    }
}
