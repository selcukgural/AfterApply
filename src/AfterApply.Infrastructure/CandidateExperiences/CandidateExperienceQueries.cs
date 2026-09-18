using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.CandidateExperiences;

/// <summary>What the author-facing service and the admin service share: the child-row loader
/// behind every projection, and the cache keys a write has to evict. One place, so an admin
/// delete evicts exactly what an owner delete evicts.</summary>
internal sealed class CandidateExperienceQueries(AppDbContext dbContext, ICompanyCacheInvalidator invalidator)
{
    /// <summary>Everything cached for the company, not just the summary — the page, the list
    /// pages, the directory. See <see cref="ICompanyCacheInvalidator"/>.</summary>
    public ValueTask EvictSummaryAsync(Guid companyId, CancellationToken cancellationToken) =>
        invalidator.InvalidateCompanyAsync(companyId, cancellationToken);

    /// <summary>The child rows of a set of entries, grouped so a projection can be built in memory.</summary>
    public async Task<Children> LoadChildrenAsync(IEnumerable<Guid> experienceIds, CancellationToken cancellationToken)
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

    internal sealed record Children(
        ILookup<Guid, ExperienceCategoryRatingDto> Ratings,
        ILookup<Guid, (string Key, ReviewStatementKind Kind)> Picks,
        ILookup<Guid, InterviewType> Types);
}
