using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Domain.JobSearch;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSearch;

internal sealed class JobSearchSettingsService(
    AppDbContext dbContext,
    IOptions<JobSearchOptions> options,
    TimeProvider? timeProvider = null) : IJobSearchSettingsService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<EffectiveJobSearchSettings> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await dbContext.JobSearchUserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return EffectiveJobSearchSettings.Resolve(row, options.Value.ToGlobalDefaults());
    }

    public async Task<JobSearchSettingsResponse> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await dbContext.JobSearchUserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        return Build(row);
    }

    public async Task<JobSearchSettingsResponse> UpdateMineAsync(Guid userId, UpdateJobSearchPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var row = await dbContext.JobSearchUserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var created = row is null;
        row ??= JobSearchUserSettings.CreateEmpty(userId, now);

        // Only the preference columns: a body that also carries a limit is not an error, it is
        // simply not this endpoint's business — the request type has no such members.
        row.SetPreferences(request.DefaultCountry, request.DefaultLanguage, request.DefaultLocation,
            request.DefaultDatePosted?.ToString(), request.DefaultWorkFromHome, now);

        return await PersistAsync(row, created, cancellationToken);
    }

    public async Task<JobSearchSettingsResponse?> GetForUserAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == targetUserId, cancellationToken))
        {
            return null;
        }

        var row = await dbContext.JobSearchUserSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == targetUserId, cancellationToken);
        return Build(row);
    }

    public async Task<JobSearchSettingsResponse?> UpdateLimitsAsync(Guid targetUserId, UpdateJobSearchLimitsRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == targetUserId, cancellationToken))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var row = await dbContext.JobSearchUserSettings.FirstOrDefaultAsync(s => s.UserId == targetUserId, cancellationToken);
        var created = row is null;
        row ??= JobSearchUserSettings.CreateEmpty(targetUserId, now);

        row.SetLimits(request.PerUserDailyCredits, request.MaxPagesPerSearch, request.MaxJobIdsPerDetails, now);

        return await PersistAsync(row, created, cancellationToken);
    }

    private async Task<JobSearchSettingsResponse> PersistAsync(JobSearchUserSettings row, bool created,
        CancellationToken cancellationToken)
    {
        if (row.IsEmpty)
        {
            // Nothing overridden any more: the row would only say "use the defaults", which its
            // absence already says.
            if (!created)
            {
                dbContext.JobSearchUserSettings.Remove(row);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Build(null);
        }

        if (created)
        {
            dbContext.JobSearchUserSettings.Add(row);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Build(row);
    }

    private JobSearchSettingsResponse Build(JobSearchUserSettings? row)
    {
        var global = options.Value.ToGlobalDefaults();
        var overrides = row is null
            ? null
            : new JobSearchUserOverridesResponse(
                row.DefaultCountry,
                row.DefaultLanguage,
                row.DefaultLocation,
                row.DefaultDatePosted is { } stored && Enum.TryParse<JobSearchDatePosted>(stored, ignoreCase: true, out var parsed)
                    ? parsed
                    : null,
                row.DefaultWorkFromHome,
                row.PerUserDailyCredits,
                row.MaxPagesPerSearch,
                row.MaxJobIdsPerDetails,
                row.UpdatedAt);

        return new JobSearchSettingsResponse(EffectiveJobSearchSettings.Resolve(row, global), overrides, global);
    }
}
