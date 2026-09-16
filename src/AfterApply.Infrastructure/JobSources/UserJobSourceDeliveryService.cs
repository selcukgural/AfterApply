using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Domain.Common;
using AfterApply.Domain.Applications;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// What a user can see of the shared posting table: exactly the rows delivered to them, and
/// nothing by id alone — every query here starts from <c>UserJobSourceDeliveries</c> filtered
/// by the caller, so another user's posting id is a 404, not a leak.
/// </summary>
internal sealed class UserJobSourceDeliveryService(
    AppDbContext dbContext,
    IApplicationService applications,
    TimeProvider? timeProvider = null) : IUserJobSourceDeliveryService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<JobSourceDeliveriesResponse> ListAsync(Guid userId, int? weekKey, int take, CancellationToken cancellationToken)
    {
        var runs = dbContext.UserJobSourceRuns.AsNoTracking().Where(r => r.UserId == userId);
        var run = weekKey is { } week
            ? await runs.SingleOrDefaultAsync(r => r.WeekKey == week, cancellationToken)
            : await runs.OrderByDescending(r => r.WeekKey).FirstOrDefaultAsync(cancellationToken);

        var effectiveWeek = weekKey ?? run?.WeekKey;
        if (effectiveWeek is null)
        {
            return new JobSourceDeliveriesResponse([], null);
        }

        var minScore = await dbContext.UserJobSourceProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.MinScore)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var deliveries = dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.WeekKey == effectiveWeek);

        // The threshold is applied here, at read time: an unscored posting is never hidden by it
        // (there is no score to be below), and moving it re-reads the same rows.
        var items = await deliveries
            .Where(d => d.Score == null || d.Score >= minScore)
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => new { d, p })
            // Scored postings first, best first; the unscored keep the sweep's order behind them.
            .OrderByDescending(x => x.d.Score.HasValue)
            .ThenByDescending(x => x.d.Score)
            .ThenBy(x => x.d.Rank)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new JobSourcePostingSummaryResponse(
                x.p.Id, x.p.Source, x.p.Title, x.p.CompanyName, x.p.CompanyProfileUrl, x.p.Location, x.p.PostedAt, x.p.Url,
                x.p.Seniority, x.p.EmploymentType, x.d.DeliveredAt, x.d.WeekKey, x.d.Score, x.d.ScoreSummary))
            .ToListAsync(cancellationToken);

        if (run is null)
        {
            return new JobSourceDeliveriesResponse(items, null);
        }

        var scoredCount = await deliveries.CountAsync(d => d.Score != null, cancellationToken);
        var hidden = minScore == 0 ? 0 : await deliveries.CountAsync(d => d.Score != null && d.Score < minScore, cancellationToken);

        return new JobSourceDeliveriesResponse(items,
            new JobSourceRunResponse(run.WeekKey, run.RanAt, run.CandidateCount, run.DeliveredCount,
                run.ExcludedAppliedCount, run.ExcludedRecentlyShownCount, scoredCount, hidden));
    }

    public Task<JobSourcePostingDetailResponse?> GetAsync(Guid userId, Guid postingId, CancellationToken cancellationToken) =>
        dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.PostingId == postingId)
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => new JobSourcePostingDetailResponse(
                p.Id, p.Source, p.Title, p.CompanyName, p.CompanyProfileUrl, p.Location, p.PostedAt, p.Url, p.Description,
                p.Seniority, p.EmploymentType, p.JobFunction, p.Industries, d.DeliveredAt, d.WeekKey,
                d.Score, d.ScoreSummary, d.MatchedCriteria, d.MissingCriteria, d.RequiredSkills, d.ScoredAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ApplicationDetailResponse?> MarkAppliedAsync(Guid userId, Guid postingId, CancellationToken cancellationToken)
    {
        var posting = await dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.PostingId == postingId)
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => p)
            .SingleOrDefaultAsync(cancellationToken);
        if (posting is null)
        {
            return null;
        }

        // The same URL twice is the same application: the button can be pressed twice, and the
        // sweep's applied-exclusion already keys on this URL, so one row is the truth.
        var existingId = await dbContext.Applications
            .Where(a => a.UserId == userId && a.JobUrl == posting.Url)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId is { } id)
        {
            return await applications.GetByIdAsync(userId, id, cancellationToken);
        }

        // Through the ordinary create path — company resolution, cache invalidation, the lot —
        // so an application that started here is indistinguishable from one typed in by hand,
        // except for its source. Employment type is not something the listing states reliably;
        // the user can correct it on the application, as with the extension's rows.
        return await applications.CreateAsync(userId, new CreateApplicationRequest(
            posting.CompanyName, posting.Title, posting.Url, posting.Location, EmploymentType.FullTime,
            _timeProvider.GetUtcNow(), posting.Source, Notes: null), cancellationToken);
    }

    public async Task<JobSourceStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var activeUntil = await dbContext.ProEntitlements.AsNoTracking()
            .Where(e => e.UserId == userId && e.RevokedAt == null && e.ActiveUntil > now)
            .Select(e => (DateTimeOffset?)e.ActiveUntil)
            .FirstOrDefaultAsync(cancellationToken);
        var cv = await dbContext.CvDocuments.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.IsDefault).ThenByDescending(d => d.UploadedAt)
            .Select(d => d.FileName)
            .FirstOrDefaultAsync(cancellationToken);
        var hasProfile = await dbContext.UserJobSourceProfiles.AnyAsync(p => p.UserId == userId, cancellationToken);
        var announcementDismissed = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.WeeklyJobsAnnouncementDismissedAt != null)
            .FirstOrDefaultAsync(cancellationToken);
        return new JobSourceStatusResponse(activeUntil is not null, activeUntil, cv is not null, cv, hasProfile, announcementDismissed);
    }

    public async Task DismissAnnouncementAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        await dbContext.Users
            .Where(u => u.Id == userId && u.WeeklyJobsAnnouncementDismissedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.WeeklyJobsAnnouncementDismissedAt, now), cancellationToken);
    }
}
