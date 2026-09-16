using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The user's criteria, and the resolution of each title to the shared query it maps to. A query
/// row is created on first use and never deleted here — another profile may point at it, and an
/// orphan costs nothing because the sweep only runs queries an eligible profile references.
/// </summary>
internal sealed class UserJobSourceProfileService(AppDbContext dbContext, IOptions<JobSourceOptions> options, TimeProvider? timeProvider = null)
    : IUserJobSourceProfileService
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
            // The consent is the price of entry, not an option: the feature is the score, and the
            // score is the CV going to the model. A create without it is refused as a validation
            // error rather than silently saving an unscorable profile. Once recorded it stays
            // recorded, so a later save need not repeat it.
            if (!request.AcceptAiScoring)
            {
                throw new JobSourceAiConsentRequiredException();
            }

            profile = UserJobSourceProfile.Create(userId, location, request.RemoteOnly, request.Enabled, request.MinScore, request.EmailDigest, now);
            dbContext.UserJobSourceProfiles.Add(profile);
        }
        else
        {
            profile.Update(location, request.RemoteOnly, request.Enabled, request.MinScore, request.EmailDigest, now);
        }

        if (request.AcceptAiScoring)
        {
            profile.AcceptAiScoring(now);
        }

        // One shared query per (title, source): the sweep runs each source's query on that
        // source, and the delivery round-robins across all of them.
        var links = new List<(string Title, Guid QueryId)>();
        foreach (var title in titles)
        {
            foreach (var source in EnabledSources)
            {
                var query = await ResolveQueryAsync(source, title, location, request.RemoteOnly, now, cancellationToken);
                links.Add((title, query.Id));
            }
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

    private IEnumerable<Source> EnabledSources =>
        options.Value.KariyerNetEnabled ? [Source.LinkedIn, Source.KariyerNet] : [Source.LinkedIn];

    private async Task<JobSourceQuery> ResolveQueryAsync(Source source, string title, string location, bool remoteOnly, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var keyHash = JobSourceQueryNormalizer.KeyHash(source, title, location, DefaultWindow, remoteOnly);
        var existing = dbContext.JobSourceQueries.Local.FirstOrDefault(q => q.KeyHash == keyHash)
                       ?? await dbContext.JobSourceQueries.SingleOrDefaultAsync(q => q.KeyHash == keyHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var query = JobSourceQuery.Create(source, JobSourceQueryNormalizer.NormalizeText(title),
            JobSourceQueryNormalizer.NormalizeText(location), DefaultWindow, remoteOnly, keyHash, now);
        dbContext.JobSourceQueries.Add(query);
        return query;
    }

    private static string Collapse(string value) => JobSourceQueryNormalizer.CollapseWhitespace(value);

    private static JobSourceProfileResponse ToResponse(UserJobSourceProfile profile) =>
        new(profile.OrderedQueries.Select(q => q.Title).Distinct().ToList(), profile.Location, profile.RemoteOnly, profile.Enabled,
            profile.MinScore, profile.AiScoringConsentAcceptedAt, profile.EmailDigestEnabled, profile.UpdatedAt);
}
