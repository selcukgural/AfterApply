using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Ai;
using AfterApply.Domain.Common;
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

        var perSource = new List<JobSourcePerSourceUsageResponse>();
        foreach (var (source, enabled) in new[] { (Source.LinkedIn, true), (Source.KariyerNet, options.Value.KariyerNetEnabled) })
        {
            var sourceRequests = await dbContext.JobSourceFetches.CountAsync(f => f.Source == source && f.At >= dayStart, cancellationToken);
            var sourceBlockedAt = await dbContext.JobSourceFetches
                .Where(f => f.Source == source && (f.Outcome == JobSourceFetchOutcome.Blocked || f.Outcome == JobSourceFetchOutcome.RateLimited))
                .MaxAsync(f => (DateTimeOffset?)f.At, cancellationToken);
            var sourceCooldown = JobSourceBudget.CooldownUntil(sourceBlockedAt, cooldown);
            perSource.Add(new JobSourcePerSourceUsageResponse(source, enabled, sourceRequests, sourceBlockedAt,
                sourceCooldown is { } su && su > now ? su : null));
        }

        var postingCount = await dbContext.JobSourcePostings.CountAsync(cancellationToken);
        var activeQueryCount = await dbContext.UserJobSourceProfiles
            .Where(p => p.Enabled).SelectMany(p => p.Queries).Select(q => q.QueryId).Distinct().CountAsync(cancellationToken);

        var cooldownUntil = JobSourceBudget.CooldownUntil(lastBlockedAt, cooldown);
        return new JobSourceUsageResponse(requestsToday, options.Value.MaxRequestsPerDay, lastBlockedAt,
            cooldownUntil is { } until && until > now ? until : null, postingCount, activeQueryCount,
            await GetScoringUsageAsync(now, dayStart, cancellationToken), perSource);
    }

    private async Task<JobFitScoringUsageResponse> GetScoringUsageAsync(DateTimeOffset now, DateTimeOffset dayStart,
        CancellationToken cancellationToken)
    {
        var scoring = options.Value.Scoring;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var callsToday = await dbContext.AiUsageEntries.CountAsync(e => e.Feature == AiFeature.JobFitScoring && e.At >= dayStart, cancellationToken);
        var month = await dbContext.AiUsageEntries
            .Where(e => e.Feature == AiFeature.JobFitScoring && e.At >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new { Input = g.Sum(e => (long)e.InputTokens), Output = g.Sum(e => (long)e.OutputTokens) })
            .FirstOrDefaultAsync(cancellationToken);
        var input = month?.Input ?? 0;
        var output = month?.Output ?? 0;
        return new JobFitScoringUsageResponse(callsToday, scoring.MaxCallsPerDay, input, output,
            JobFitScoringCost.Estimate(input, output, scoring), scoring.MonthlyBudgetUsd);
    }

    private UserJobSourceSettingsResponse ToResponse(Guid userId, int? weeklyPostingLimit) =>
        new(userId, weeklyPostingLimit, weeklyPostingLimit ?? options.Value.DefaultWeeklyPostingsPerUser);
}
