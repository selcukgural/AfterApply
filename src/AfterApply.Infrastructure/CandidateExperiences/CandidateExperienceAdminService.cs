using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CandidateExperiences;

/// <summary>Admin only — the caller has already passed <c>IAdminAccessService</c>. The one place
/// a candidate experience's author is joined to a response.</summary>
internal sealed class CandidateExperienceAdminService(
    AppDbContext dbContext,
    CandidateExperienceQueries queries,
    IOptions<CompanyReviewOptions> options) : ICandidateExperienceAdminService
{
    public async Task<PagedResult<AdminCandidateExperienceListItemResponse>> ListAsync(AdminCandidateExperienceListQuery query,
        CancellationToken cancellationToken)
    {
        var companies = dbContext.Companies.AsQueryable();
        var company = query.Company?.Trim();
        if (!string.IsNullOrEmpty(company))
        {
            var pattern = LikePattern.Contains(TurkishTextNormalizer.FoldCase(company).ToUpperInvariant());
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern, LikePattern.EscapeCharacter));
        }

        var joined = dbContext.CandidateExperiences
            .Join(companies, e => e.CompanyId, c => c.Id, (e, c) => new { e, c })
            .Join(dbContext.Users, x => x.e.UserId, u => u.Id, (x, u) => new { x.e, x.c, AuthorEmail = u.Email ?? string.Empty });

        var total = await joined.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;

        // Newest first: the table is a log of what arrived, and the company filter is how a
        // particular row is found.
        var rows = await joined
            .OrderByDescending(x => x.e.SubmittedAt).ThenByDescending(x => x.e.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var children = await queries.LoadChildrenAsync(rows.Select(r => r.e.Id), cancellationToken);

        var items = rows.Select(x =>
        {
            var e = x.e;
            return new AdminCandidateExperienceListItemResponse(
                e.Id, x.c.Id, x.c.Name, x.c.Slug, e.UserId, x.AuthorEmail, e.OverallRating,
                children.Ratings[e.Id].OrderBy(r => r.Category).ToList(),
                children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Liked).Select(p => p.Key).ToList(),
                children.Picks[e.Id].Where(p => p.Kind == ReviewStatementKind.Improve).Select(p => p.Key).ToList(),
                e.Outcome, e.Duration, e.Stages,
                children.Types[e.Id].OrderBy(t => t).ToList(),
                e.SubmittedAt, e.UpdatedAt);
        }).ToList();

        return new PagedResult<AdminCandidateExperienceListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<bool> DeleteAsync(Guid experienceId, CancellationToken cancellationToken)
    {
        var experience = await dbContext.CandidateExperiences.FirstOrDefaultAsync(e => e.Id == experienceId, cancellationToken);
        if (experience is null)
        {
            return false;
        }

        // Same removal as the owner's, evicting the same summary: the public page must drop the
        // row at once, not when the cache expires.
        dbContext.CandidateExperiences.Remove(experience);
        await dbContext.SaveChangesAsync(cancellationToken);
        await queries.EvictSummaryAsync(experience.CompanyId, cancellationToken);
        return true;
    }
}
