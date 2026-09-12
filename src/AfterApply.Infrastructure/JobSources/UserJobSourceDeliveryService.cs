using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// What a user can see of the shared posting table: exactly the rows delivered to them, and
/// nothing by id alone — every query here starts from <c>UserJobSourceDeliveries</c> filtered
/// by the caller, so another user's posting id is a 404, not a leak.
/// </summary>
internal sealed class UserJobSourceDeliveryService(AppDbContext dbContext) : IUserJobSourceDeliveryService
{
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

        var items = await dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.WeekKey == effectiveWeek)
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => new { d, p })
            .OrderBy(x => x.d.Rank)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new JobSourcePostingSummaryResponse(
                x.p.Id, x.p.Title, x.p.CompanyName, x.p.CompanyProfileUrl, x.p.Location, x.p.PostedAt, x.p.Url,
                x.p.Seniority, x.p.EmploymentType, x.d.DeliveredAt, x.d.WeekKey))
            .ToListAsync(cancellationToken);

        return new JobSourceDeliveriesResponse(items, run is null
            ? null
            : new JobSourceRunResponse(run.WeekKey, run.RanAt, run.CandidateCount, run.DeliveredCount,
                run.ExcludedAppliedCount, run.ExcludedRecentlyShownCount));
    }

    public Task<JobSourcePostingDetailResponse?> GetAsync(Guid userId, Guid postingId, CancellationToken cancellationToken) =>
        dbContext.UserJobSourceDeliveries.AsNoTracking()
            .Where(d => d.UserId == userId && d.PostingId == postingId)
            .Join(dbContext.JobSourcePostings, d => d.PostingId, p => p.Id, (d, p) => new JobSourcePostingDetailResponse(
                p.Id, p.Title, p.CompanyName, p.CompanyProfileUrl, p.Location, p.PostedAt, p.Url, p.Description,
                p.Seniority, p.EmploymentType, p.JobFunction, p.Industries, d.DeliveredAt, d.WeekKey))
            .SingleOrDefaultAsync(cancellationToken);
}
