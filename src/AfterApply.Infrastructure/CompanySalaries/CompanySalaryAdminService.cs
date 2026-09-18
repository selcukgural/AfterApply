using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanySalaries;

/// <summary>Admin only — the caller has already passed <c>IAdminAccessService</c>. The one place
/// a salary entry's author is joined to a response.</summary>
internal sealed class CompanySalaryAdminService(AppDbContext dbContext, ICompanyCacheInvalidator invalidator, IOptions<CompanyReviewOptions> options)
    : ICompanySalaryAdminService
{
    public async Task<PagedResult<AdminCompanySalaryListItemResponse>> ListAsync(AdminCompanySalaryListQuery query,
        CancellationToken cancellationToken)
    {
        var companies = dbContext.Companies.AsQueryable();
        var company = query.Company?.Trim();
        if (!string.IsNullOrEmpty(company))
        {
            var pattern = $"%{TurkishTextNormalizer.FoldCase(company).ToUpperInvariant()}%";
            companies = companies.Where(c => EF.Functions.ILike(c.NormalizedName, pattern));
        }

        var joined = dbContext.CompanySalaryEntries
            .Join(companies, s => s.CompanyId, c => c.Id, (s, c) => new { s, c })
            .Join(dbContext.Users, x => x.s.UserId, u => u.Id, (x, u) => new { x.s, x.c, AuthorEmail = u.Email ?? string.Empty })
            .Join(dbContext.Occupations, x => x.s.OccupationId, o => o.Id, (x, o) => new { x.s, x.c, x.AuthorEmail, o });

        var total = await joined.CountAsync(cancellationToken);
        var pageSize = options.Value.AdminPageSize;

        // Newest first: the table is a log of what arrived, and the company filter is how a
        // particular row is found.
        var rows = await joined
            .OrderByDescending(x => x.s.SubmittedAt).ThenByDescending(x => x.s.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new AdminCompanySalaryListItemResponse(
                x.s.Id, x.c.Id, x.c.Name, x.c.Slug, x.s.UserId, x.AuthorEmail,
                new OccupationRefResponse(x.o.Id, x.o.Code, x.o.NameTr, x.o.NameEn),
                x.s.YearsOfExperience, x.s.EmploymentType, x.s.EmploymentStatus, x.s.MonthlyNetAmount, x.s.Currency,
                x.s.AnnualBonusAmount, x.s.SubmittedAt, x.s.UpdatedAt))
            .ToList();

        return new PagedResult<AdminCompanySalaryListItemResponse>(items, total, query.Page, pageSize);
    }

    public async Task<bool> DeleteAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.CompanySalaryEntries.FirstOrDefaultAsync(s => s.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        // Same removal as the owner's, same eviction: the salary list pages sit under the
        // company's tag.
        dbContext.CompanySalaryEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await invalidator.InvalidateCompanyAsync(entry.CompanyId, cancellationToken);
        return true;
    }
}
