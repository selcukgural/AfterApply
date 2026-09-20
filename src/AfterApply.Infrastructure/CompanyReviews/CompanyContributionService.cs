using AfterApply.Application.Blog;
using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Infrastructure.Blog;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>
/// The author's three kinds of contribution as one newest-first page. Reads (id, timestamp) per
/// kind — only the kinds whose feature is on — pages them in memory (see ContributionPaging for
/// why), then hydrates the page's ids through each kind's own "mine" projection, which filters on
/// the caller's id itself, so no id from another account can ever come back.
/// </summary>
internal sealed class CompanyContributionService(
    AppDbContext dbContext,
    CompanyReviewQueries reviewQueries,
    ICompanyReviewService reviews,
    ICompanySalaryService salaries,
    ICandidateExperienceService experiences,
    IBlogCommentService blogComments,
    IOptions<CompanyReviewOptions> options,
    IOptions<CompanySalaryOptions> salaryOptions,
    IOptions<CandidateExperienceOptions> experienceOptions,
    IOptions<BlogOptions> blogOptions) : ICompanyContributionService
{
    public async Task<MyContributionsResponse> ListMineAsync(Guid userId, MyContributionsQuery query, CancellationToken cancellationToken)
    {
        var salariesOn = salaryOptions.Value.Enabled;
        var experiencesOn = experienceOptions.Value.Enabled;
        // The chips: "blog comments" is that kind alone, "company" is everything else.
        var companyKinds = query.Filter != ContributionFilter.BlogComments;
        var blogOn = blogOptions.Value.Enabled && query.Filter != ContributionFilter.Company;

        var stamps = new List<ContributionStamp>();
        if (companyKinds)
        {
            stamps.AddRange((await dbContext.CompanyReviews
                    .Where(r => r.UserId == userId)
                    .Select(r => new { r.Id, r.SubmittedAt })
                    .ToListAsync(cancellationToken))
                .Select(x => new ContributionStamp(ContributionKind.Review, x.Id, x.SubmittedAt)));
        }

        if (blogOn)
        {
            stamps.AddRange((await dbContext.BlogComments
                    .Where(c => c.UserId == userId)
                    .Select(c => new { c.Id, c.CreatedAt })
                    .ToListAsync(cancellationToken))
                .Select(x => new ContributionStamp(ContributionKind.BlogComment, x.Id, x.CreatedAt)));
        }

        if (salariesOn && companyKinds)
        {
            stamps.AddRange((await dbContext.CompanySalaryEntries
                    .Where(s => s.UserId == userId)
                    .Select(s => new { s.Id, s.SubmittedAt })
                    .ToListAsync(cancellationToken))
                .Select(x => new ContributionStamp(ContributionKind.Salary, x.Id, x.SubmittedAt)));
        }

        if (experiencesOn && companyKinds)
        {
            stamps.AddRange((await dbContext.CandidateExperiences
                    .Where(e => e.UserId == userId)
                    .Select(e => new { e.Id, e.SubmittedAt })
                    .ToListAsync(cancellationToken))
                .Select(x => new ContributionStamp(ContributionKind.Experience, x.Id, x.SubmittedAt)));
        }

        var pageSize = options.Value.ContributionsPageSize;
        var (page, total) = ContributionPaging.Page(stamps, query.Page, pageSize);

        var reviewRows = await HydrateAsync(page, ContributionKind.Review,
            ids => reviews.ListMineByIdsAsync(userId, ids, cancellationToken), r => r.Id);
        var salaryRows = await HydrateAsync(page, ContributionKind.Salary,
            ids => salaries.ListMineByIdsAsync(userId, ids, cancellationToken), s => s.Id);
        var experienceRows = await HydrateAsync(page, ContributionKind.Experience,
            ids => experiences.ListMineByIdsAsync(userId, ids, cancellationToken), e => e.Id);
        var commentRows = await HydrateAsync(page, ContributionKind.BlogComment,
            ids => blogComments.ListMineByIdsAsync(userId, ids, cancellationToken), c => c.Id);

        // A row deleted between the stamp read and the hydration simply drops out of the page.
        var items = page
            .Select(stamp => stamp.Kind switch
            {
                ContributionKind.Review when reviewRows.TryGetValue(stamp.Id, out var review) =>
                    new MyContributionResponse(stamp.Kind, stamp.SubmittedAt, review, null, null),
                ContributionKind.Salary when salaryRows.TryGetValue(stamp.Id, out var salary) =>
                    new MyContributionResponse(stamp.Kind, stamp.SubmittedAt, null, salary, null),
                ContributionKind.Experience when experienceRows.TryGetValue(stamp.Id, out var experience) =>
                    new MyContributionResponse(stamp.Kind, stamp.SubmittedAt, null, null, experience),
                ContributionKind.BlogComment when commentRows.TryGetValue(stamp.Id, out var comment) =>
                    new MyContributionResponse(stamp.Kind, stamp.SubmittedAt, null, null, null, comment),
                _ => null
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        return new MyContributionsResponse(
            items,
            total,
            query.Page,
            pageSize,
            await reviewQueries.GetQuotaAsync(userId, cancellationToken),
            salariesOn ? await salaries.GetQuotaAsync(userId, cancellationToken) : null,
            experiencesOn ? await experiences.GetQuotaAsync(userId, cancellationToken) : null);
    }

    private static async Task<Dictionary<Guid, T>> HydrateAsync<T>(IReadOnlyList<ContributionStamp> page, ContributionKind kind,
        Func<IReadOnlyCollection<Guid>, Task<IReadOnlyList<T>>> load, Func<T, Guid> id)
    {
        var ids = page.Where(s => s.Kind == kind).Select(s => s.Id).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return (await load(ids)).ToDictionary(id);
    }
}
