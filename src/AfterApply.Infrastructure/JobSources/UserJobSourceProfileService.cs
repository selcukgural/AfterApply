using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The user's criteria, and the resolution of each title to the shared query it maps to. A query
/// row is created on first use and never deleted here — another profile may point at it, and an
/// orphan costs nothing because the sweep only runs queries an eligible profile references.
/// </summary>
internal sealed class UserJobSourceProfileService(AppDbContext dbContext, TimeProvider? timeProvider = null) : IUserJobSourceProfileService
{
    private const JobSourceTimeWindow DefaultWindow = JobSourceTimeWindow.Week;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<JobSourceProfileResponse?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserJobSourceProfiles.AsNoTracking()
            .Include(p => p.Queries)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? null : ToResponse(profile);
    }

    public async Task<JobSourceProfileResponse> UpsertAsync(Guid userId, UpsertJobSourceProfileRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var location = Collapse(request.Location);
        var titles = request.Titles.Select(Collapse).Where(t => t.Length > 0)
            .DistinctBy(JobSourceQueryNormalizer.NormalizeText).Take(UserJobSourceProfile.MaxTitles).ToList();

        var profile = await dbContext.UserJobSourceProfiles
            .Include(p => p.Queries)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = UserJobSourceProfile.Create(userId, location, request.RemoteOnly, request.Enabled, now);
            dbContext.UserJobSourceProfiles.Add(profile);
        }
        else
        {
            profile.Update(location, request.RemoteOnly, request.Enabled, now);
        }

        var links = new List<(string Title, Guid QueryId)>();
        foreach (var title in titles)
        {
            var query = await ResolveQueryAsync(title, location, request.RemoteOnly, now, cancellationToken);
            links.Add((title, query.Id));
        }

        profile.SetQueries(links);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserJobSourceProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        dbContext.UserJobSourceProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<JobSourceQuery> ResolveQueryAsync(string title, string location, bool remoteOnly, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var keyHash = JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, title, location, DefaultWindow, remoteOnly);
        var existing = dbContext.JobSourceQueries.Local.FirstOrDefault(q => q.KeyHash == keyHash)
                       ?? await dbContext.JobSourceQueries.SingleOrDefaultAsync(q => q.KeyHash == keyHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var query = JobSourceQuery.Create(Source.LinkedIn, JobSourceQueryNormalizer.NormalizeText(title),
            JobSourceQueryNormalizer.NormalizeText(location), DefaultWindow, remoteOnly, keyHash, now);
        dbContext.JobSourceQueries.Add(query);
        return query;
    }

    private static string Collapse(string value) => JobSourceQueryNormalizer.CollapseWhitespace(value);

    private static JobSourceProfileResponse ToResponse(UserJobSourceProfile profile) =>
        new(profile.OrderedQueries.Select(q => q.Title).ToList(), profile.Location, profile.RemoteOnly, profile.Enabled, profile.UpdatedAt);
}
