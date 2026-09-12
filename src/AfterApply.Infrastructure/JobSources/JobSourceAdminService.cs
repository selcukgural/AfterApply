using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

internal sealed class JobSourceAdminService(AppDbContext dbContext, IOptions<JobSourceOptions> options, TimeProvider? timeProvider = null)
    : IJobSourceAdminService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<UserJobSourceSettingsResponse> GetUserSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.UserJobSourceSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return ToResponse(userId, settings?.WeeklyPostingLimit);
    }

    public async Task<UserJobSourceSettingsResponse> UpdateUserLimitsAsync(Guid userId, UpdateUserJobSourceLimitsRequest request,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var settings = await dbContext.UserJobSourceSettings.SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (settings is null)
        {
            if (request.WeeklyPostingLimit is not null)
            {
                dbContext.UserJobSourceSettings.Add(UserJobSourceSettings.Create(userId, request.WeeklyPostingLimit, now));
            }
        }
        else
        {
            settings.Update(request.WeeklyPostingLimit, now);
            if (settings.IsEmpty)
            {
                // Nothing overridden any more: the row would only say "default", which its absence says better.
                dbContext.UserJobSourceSettings.Remove(settings);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(userId, request.WeeklyPostingLimit);
    }

    public async Task<JobSourceUsageResponse> GetUsageAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var cooldown = TimeSpan.FromHours(options.Value.CircuitCooldownHours);

        var requestsToday = await dbContext.JobSourceFetches.CountAsync(f => f.At >= dayStart, cancellationToken);
        var lastBlockedAt = await dbContext.JobSourceFetches
            .Where(f => f.Outcome == JobSourceFetchOutcome.Blocked || f.Outcome == JobSourceFetchOutcome.RateLimited)
            .MaxAsync(f => (DateTimeOffset?)f.At, cancellationToken);
        var postingCount = await dbContext.JobSourcePostings.CountAsync(cancellationToken);
        var activeQueryCount = await dbContext.UserJobSourceProfiles
            .Where(p => p.Enabled).SelectMany(p => p.Queries).Select(q => q.QueryId).Distinct().CountAsync(cancellationToken);

        var cooldownUntil = JobSourceBudget.CooldownUntil(lastBlockedAt, cooldown);
        return new JobSourceUsageResponse(requestsToday, options.Value.MaxRequestsPerDay, lastBlockedAt,
            cooldownUntil is { } until && until > now ? until : null, postingCount, activeQueryCount);
    }

    private UserJobSourceSettingsResponse ToResponse(Guid userId, int? weeklyPostingLimit) =>
        new(userId, weeklyPostingLimit, weeklyPostingLimit ?? options.Value.DefaultWeeklyPostingsPerUser);
}
