using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CandidateExperiences;

internal sealed class CandidateExperienceService(AppDbContext dbContext, HybridCache cache, IOptions<CandidateExperienceOptions> options)
    : ICandidateExperienceService
{
    private const string GlobalAverageCacheKey = "candidate-experiences:global-average";

    private static readonly HybridCacheEntryOptions GlobalAverageCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private static readonly HybridCacheEntryOptions SummaryCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60)
    };

    public static string SummaryCacheKey(Guid companyId) => $"candidate-experiences:summary:{companyId}";

    public async Task<CandidateExperienceViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        var own = await ProjectMineAsync(
            dbContext.CandidateExperiences.Where(e => e.UserId == userId && e.CompanyId == companyId), cancellationToken);
        return new CandidateExperienceViewerStateResponse(own.SingleOrDefault(), await GetQuotaAsync(userId, cancellationToken));
    }

    public async Task<CandidateExperiencePageResponse?> ListForCompanyAsync(string slug, CandidateExperienceListQuery query, CancellationToken cancellationToken)
    {
        var companyId = await dbContext.Companies
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (companyId is null)
        {
            return null;
        }

        var entries = dbContext.CandidateExperiences.Where(e => e.CompanyId == companyId);
        var total = await entries.CountAsync(cancellationToken);
        var pageSize = options.Value.PageSize;

        // Ordered before the projection, and only the columns the public record has a place for:
        // the author is never selected. SubmittedAt orders the page and becomes a quarter label in
        // memory — the exact date does not leave this method.
        var rows = await entries
            .OrderByDescending(e => e.SubmittedAt).ThenBy(e => e.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new { e.Id, e.OverallRating, e.Outcome, e.Duration, e.Stages, e.SubmittedAt })
            .ToListAsync(cancellationToken);
        var children = await LoadChildrenAsync(rows.Select(r => r.Id), cancellationToken);

        var items = rows.Select(e => new CandidateExperiencePublicResponse(
            e.Id, e.OverallRating,
            children.Ratings[e.Id].OrderBy(r => r.Category).ToList(),
            children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Liked).Select(p => p.Key).ToList(),
            children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Improve).Select(p => p.Key).ToList(),
            e.Outcome, e.Duration, e.Stages,
            children.Types[e.Id].OrderBy(t => t).ToList(),
            CandidateExperienceStats.Quarter(e.SubmittedAt))).ToList();

        var summary = await GetSummaryAsync(companyId.Value, cancellationToken);
        return new CandidateExperiencePageResponse(items, total, query.Page, pageSize, summary);
    }

    public async Task<MyCandidateExperienceResponse?> CreateAsync(Guid userId, Guid companyId, CandidateExperienceRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        // Checked before the insert so the user gets the right message; the unique index is what
        // actually guarantees it against two requests racing.
        if (await dbContext.CandidateExperiences.AnyAsync(e => e.UserId == userId && e.CompanyId == companyId, cancellationToken))
        {
            throw new CandidateExperienceAlreadyExistsException();
        }

        var quota = await GetQuotaAsync(userId, cancellationToken);
        if (quota.Used >= quota.Limit)
        {
            throw new CandidateExperienceQuotaReachedException(quota.Limit);
        }

        var content = ToContent(request);
        var experience = CandidateExperience.Create(userId, companyId, content, DateTimeOffset.UtcNow);
        dbContext.CandidateExperiences.Add(experience);
        AddChildren(experience.Id, content);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CandidateExperienceAlreadyExistsException();
        }

        await EvictSummaryAsync(companyId, cancellationToken);
        return (await ProjectMineAsync(dbContext.CandidateExperiences.Where(e => e.Id == experience.Id), cancellationToken)).Single();
    }

    public async Task<MyCandidateExperienceResponse?> UpdateAsync(Guid userId, Guid experienceId, CandidateExperienceRequest request, CancellationToken cancellationToken)
    {
        // Filtered on the caller: someone else's entry is "not found", never "forbidden".
        var experience = await dbContext.CandidateExperiences
            .FirstOrDefaultAsync(e => e.Id == experienceId && e.UserId == userId, cancellationToken);
        if (experience is null)
        {
            return null;
        }

        var content = ToContent(request);

        // The child rows are replaced wholesale, in one transaction with the row itself, so a
        // failure halfway leaves the old entry intact rather than an entry with no picks.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        experience.Edit(content, DateTimeOffset.UtcNow);
        await dbContext.CandidateExperienceCategoryRatings.Where(c => c.ExperienceId == experience.Id).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CandidateExperienceStatementPicks.Where(p => p.ExperienceId == experience.Id).ExecuteDeleteAsync(cancellationToken);
        await dbContext.CandidateExperienceInterviewTypes.Where(t => t.ExperienceId == experience.Id).ExecuteDeleteAsync(cancellationToken);
        AddChildren(experience.Id, content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await EvictSummaryAsync(experience.CompanyId, cancellationToken);
        return (await ProjectMineAsync(dbContext.CandidateExperiences.Where(e => e.Id == experience.Id), cancellationToken)).Single();
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid experienceId, CancellationToken cancellationToken)
    {
        var experience = await dbContext.CandidateExperiences
            .FirstOrDefaultAsync(e => e.Id == experienceId && e.UserId == userId, cancellationToken);
        if (experience is null)
        {
            return false;
        }

        dbContext.CandidateExperiences.Remove(experience);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EvictSummaryAsync(experience.CompanyId, cancellationToken);
        return true;
    }

    public async Task<MyCandidateExperiencesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await ProjectMineAsync(
            dbContext.CandidateExperiences.Where(e => e.UserId == userId).OrderByDescending(e => e.SubmittedAt), cancellationToken);
        return new MyCandidateExperiencesResponse(items, await GetQuotaAsync(userId, cancellationToken));
    }

    /// <summary>The aggregate a public page shows. Cached a minute per company; every write
    /// evicts it.</summary>
    private ValueTask<CandidateExperienceSummaryResponse> GetSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(SummaryCacheKey(companyId), async ct =>
        {
            // The whole company, not the page: a median of page two is not a median. Bounded by
            // one entry per contributor, so it stays a short list.
            var rows = await dbContext.CandidateExperiences
                .Where(e => e.CompanyId == companyId)
                .Select(e => new { e.Id, e.OverallRating, e.Outcome, e.Duration, e.Stages })
                .ToListAsync(ct);
            var children = await LoadChildrenAsync(rows.Select(r => r.Id), ct);
            var aggregate = rows.Select(e => new ExperienceAggregateRow(
                e.OverallRating, e.Outcome, e.Duration, e.Stages,
                children.Ratings[e.Id].Select(r => new ExperienceCategoryRating(r.Category, r.Rating)).ToList(),
                children.Picks[e.Id].ToList(),
                children.Types[e.Id].ToList())).ToList();

            var globalAverage = await GetGlobalAverageAsync(ct);
            return CandidateExperienceStats.Build(aggregate, globalAverage, options.Value.MinimumEntriesForStats, options.Value.PriorWeight);
        }, SummaryCacheOptions, cancellationToken: cancellationToken);

    /// <summary>Mean Overall rating over every experience on the site — the prior the score pulls
    /// toward. Falls back to the scale's midpoint while there is none.</summary>
    private ValueTask<double> GetGlobalAverageAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(GlobalAverageCacheKey, async ct =>
            await dbContext.CandidateExperiences.AnyAsync(ct)
                ? await dbContext.CandidateExperiences.AverageAsync(e => (double)e.OverallRating, ct)
                : CompanyReviewScoring.NeutralAverage,
            GlobalAverageCacheOptions, cancellationToken: cancellationToken);

    private async Task EvictSummaryAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(SummaryCacheKey(companyId), cancellationToken);
        await cache.RemoveAsync(GlobalAverageCacheKey, cancellationToken);
    }

    private async Task<ExperienceQuotaResponse> GetQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var used = await dbContext.CandidateExperiences.CountAsync(e => e.UserId == userId, cancellationToken);
        return new ExperienceQuotaResponse(used, options.Value.MaxEntriesPerUser);
    }

    private async Task<List<MyCandidateExperienceResponse>> ProjectMineAsync(IQueryable<CandidateExperience> entries, CancellationToken cancellationToken)
    {
        var rows = await entries
            .Join(dbContext.Companies, e => e.CompanyId, c => c.Id, (e, c) => new { Entry = e, c.Slug, c.Name })
            .ToListAsync(cancellationToken);
        var children = await LoadChildrenAsync(rows.Select(r => r.Entry.Id), cancellationToken);

        return rows.Select(x =>
        {
            var e = x.Entry;
            return new MyCandidateExperienceResponse(
                e.Id, e.CompanyId, x.Slug ?? string.Empty, x.Name, e.OverallRating,
                children.Ratings[e.Id].OrderBy(r => r.Category).ToList(),
                children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Liked).Select(p => p.Key).ToList(),
                children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Improve).Select(p => p.Key).ToList(),
                e.Outcome, e.Duration, e.Stages,
                children.Types[e.Id].OrderBy(t => t).ToList(),
                e.SubmittedAt, e.UpdatedAt);
        }).ToList();
    }

    /// <summary>The child rows of a set of entries, grouped so a projection can be built in memory.</summary>
    private async Task<Children> LoadChildrenAsync(IEnumerable<Guid> experienceIds, CancellationToken cancellationToken)
    {
        var ids = experienceIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Children(
                Enumerable.Empty<ExperienceCategoryRatingDto>().ToLookup(_ => Guid.Empty),
                Enumerable.Empty<(string, ReviewStatementKind)>().ToLookup(_ => Guid.Empty),
                Enumerable.Empty<InterviewType>().ToLookup(_ => Guid.Empty));
        }

        var ratings = await dbContext.CandidateExperienceCategoryRatings
            .Where(c => ids.Contains(c.ExperienceId))
            .Select(c => new { c.ExperienceId, c.Category, c.Rating })
            .ToListAsync(cancellationToken);

        // Ids are version-7 GUIDs, so ordering by Id is the order the author picked them in.
        var picks = await dbContext.CandidateExperienceStatementPicks
            .Where(p => ids.Contains(p.ExperienceId))
            .OrderBy(p => p.Id)
            .Select(p => new { p.ExperienceId, p.StatementKey, p.Kind })
            .ToListAsync(cancellationToken);

        var types = await dbContext.CandidateExperienceInterviewTypes
            .Where(t => ids.Contains(t.ExperienceId))
            .Select(t => new { t.ExperienceId, t.Type })
            .ToListAsync(cancellationToken);

        return new Children(
            ratings.ToLookup(x => x.ExperienceId, x => new ExperienceCategoryRatingDto(x.Category, x.Rating)),
            picks.ToLookup(x => x.ExperienceId, x => (x.StatementKey, x.Kind)),
            types.ToLookup(x => x.ExperienceId, x => x.Type));
    }

    /// <summary>Content the domain has already validated: every key resolves.</summary>
    private void AddChildren(Guid experienceId, CandidateExperienceContent content)
    {
        dbContext.CandidateExperienceCategoryRatings.AddRange(
            content.CategoryRatings.Select(c => CandidateExperienceCategoryRating.Create(experienceId, c.Category, c.Rating)));
        dbContext.CandidateExperienceStatementPicks.AddRange(
            content.Statements().Select(statement => CandidateExperienceStatementPick.Create(experienceId, statement)));
        dbContext.CandidateExperienceInterviewTypes.AddRange(
            content.InterviewTypes.Select(type => CandidateExperienceInterviewType.Create(experienceId, type)));
    }

    private static CandidateExperienceContent ToContent(CandidateExperienceRequest r) => new(
        r.OverallRating,
        (r.CategoryRatings ?? []).Select(x => new ExperienceCategoryRating(x.Category, x.Rating)).ToList(),
        r.LikedStatements ?? [],
        r.ImprovableStatements ?? [],
        r.Outcome, r.Duration, r.Stages,
        r.InterviewTypes ?? []);

    private sealed record Children(
        ILookup<Guid, ExperienceCategoryRatingDto> Ratings,
        ILookup<Guid, (string Key, ReviewStatementKind Kind)> Picks,
        ILookup<Guid, InterviewType> Types);
}
