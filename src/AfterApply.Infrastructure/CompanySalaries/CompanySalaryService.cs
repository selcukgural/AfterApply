using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanySalaries;

internal sealed class CompanySalaryService(AppDbContext dbContext, CompanySlugAllocator slugAllocator, IOptions<CompanySalaryOptions> options)
    : ICompanySalaryService
{
    public async Task<CompanySalaryViewerStateResponse?> GetViewerStateAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null)
        {
            return null;
        }

        // The directory lists a company by its slug; a row that still lacks one gets it now, so
        // this contribution shows up there the way a review would.
        await slugAllocator.EnsureSlugAsync(company, cancellationToken);

        var own = await ProjectMineAsync(
            dbContext.CompanySalaryEntries.Where(s => s.UserId == userId && s.CompanyId == companyId).OrderByDescending(s => s.SubmittedAt),
            cancellationToken);
        return new CompanySalaryViewerStateResponse(own, await GetQuotaAsync(userId, cancellationToken));
    }

    public async Task<CompanySalaryPageResponse?> ListForCompanyAsync(Guid companyId, CompanySalaryListQuery query, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        var entries = dbContext.CompanySalaryEntries.Where(s => s.CompanyId == companyId);
        var total = await entries.CountAsync(cancellationToken);
        var pageSize = options.Value.PageSize;

        // Only the columns the public record has a place for: the years and the author are not
        // selected at all. The band and the month are derived in memory — EF cannot translate
        // either, and a page is at most PageSize rows.
        var rows = await entries
            .OrderByDescending(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Join(dbContext.Occupations, s => s.OccupationId, o => o.Id, (s, o) => new
            {
                s.Id, s.YearsOfExperience, s.EmploymentType, s.EmploymentStatus,
                s.MonthlyNetAmount, s.Currency, s.AnnualBonusAmount, s.SubmittedAt,
                Occupation = new OccupationRefResponse(o.Id, o.Code, o.NameTr, o.NameEn)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(s => new CompanySalaryPublicResponse(
            s.Id, s.Occupation, ExperienceBands.From(s.YearsOfExperience), s.EmploymentType, s.EmploymentStatus,
            s.MonthlyNetAmount, s.Currency, s.AnnualBonusAmount,
            // Month precision on purpose — see CompanySalaryPublicResponse.
            s.SubmittedAt.ToString("yyyy-MM"))).ToList();

        // The whole company's amounts, not the page's: a median of page two is not a median.
        // Bounded by the quota times the number of contributors, so it stays a short list.
        var amounts = await entries
            .Select(s => new { s.Currency, s.MonthlyNetAmount })
            .ToListAsync(cancellationToken);
        var minimum = options.Value.MinimumEntriesForStats;
        var stats = amounts
            .GroupBy(a => a.Currency)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var values = g.Select(a => a.MonthlyNetAmount).ToList();
                var enough = values.Count >= minimum;
                return new SalaryCurrencyStatResponse(
                    g.Key, values.Count,
                    enough ? SalaryStats.Median(values) : null,
                    enough ? values.Min() : null,
                    enough ? values.Max() : null);
            })
            .ToList();

        return new CompanySalaryPageResponse(items, total, query.Page, pageSize, stats, minimum);
    }

    public async Task<MyCompanySalaryResponse?> CreateAsync(Guid userId, Guid companyId, CompanySalaryRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        await EnsureOccupationAsync(request.OccupationId, cancellationToken);

        // Checked before the insert so the user gets the right message; the unique index is what
        // actually guarantees it against two requests racing.
        if (await dbContext.CompanySalaryEntries.AnyAsync(
                s => s.UserId == userId && s.CompanyId == companyId && s.OccupationId == request.OccupationId, cancellationToken))
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        var quota = await GetQuotaAsync(userId, cancellationToken);
        if (quota.Used >= quota.Limit)
        {
            throw new CompanySalaryQuotaReachedException(quota.Limit);
        }

        var entry = CompanySalaryEntry.Create(userId, companyId, ToContent(request), DateTimeOffset.UtcNow);
        dbContext.CompanySalaryEntries.Add(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        return (await ProjectMineAsync(dbContext.CompanySalaryEntries.Where(s => s.Id == entry.Id), cancellationToken)).Single();
    }

    public async Task<MyCompanySalaryResponse?> UpdateAsync(Guid userId, Guid entryId, CompanySalaryRequest request, CancellationToken cancellationToken)
    {
        // Filtered on the caller: someone else's entry is "not found", never "forbidden".
        var entry = await dbContext.CompanySalaryEntries
            .FirstOrDefaultAsync(s => s.Id == entryId && s.UserId == userId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        await EnsureOccupationAsync(request.OccupationId, cancellationToken);
        if (await dbContext.CompanySalaryEntries.AnyAsync(
                s => s.UserId == userId && s.CompanyId == entry.CompanyId && s.OccupationId == request.OccupationId && s.Id != entry.Id,
                cancellationToken))
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        entry.Edit(ToContent(request), DateTimeOffset.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        return (await ProjectMineAsync(dbContext.CompanySalaryEntries.Where(s => s.Id == entry.Id), cancellationToken)).Single();
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await dbContext.CompanySalaryEntries
            .FirstOrDefaultAsync(s => s.Id == entryId && s.UserId == userId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        dbContext.CompanySalaryEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MySalariesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await ProjectMineAsync(
            dbContext.CompanySalaryEntries.Where(s => s.UserId == userId).OrderByDescending(s => s.SubmittedAt), cancellationToken);
        return new MySalariesResponse(items, await GetQuotaAsync(userId, cancellationToken));
    }

    public async Task<IReadOnlyList<MyCompanySalaryResponse>> ListMineByIdsAsync(Guid userId, IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        await ProjectMineAsync(dbContext.CompanySalaryEntries.Where(s => s.UserId == userId && ids.Contains(s.Id)), cancellationToken);

    public async Task<SalaryQuotaResponse> GetQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var used = await dbContext.CompanySalaryEntries.CountAsync(s => s.UserId == userId, cancellationToken);
        return new SalaryQuotaResponse(used, options.Value.MaxEntriesPerUser);
    }

    // Anonymous rows mapped in memory, ordered before the projection — see CompanyReviewQueries
    // for why nothing is ordered after a constructor projection.
    private async Task<List<MyCompanySalaryResponse>> ProjectMineAsync(IQueryable<CompanySalaryEntry> entries, CancellationToken cancellationToken)
    {
        var rows = await entries
            .Join(dbContext.Companies, s => s.CompanyId, c => c.Id, (s, c) => new { Entry = s, c.Slug, c.Name })
            .Join(dbContext.Occupations, x => x.Entry.OccupationId, o => o.Id,
                (x, o) => new { x.Entry, x.Slug, x.Name, Occupation = new OccupationRefResponse(o.Id, o.Code, o.NameTr, o.NameEn) })
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var s = x.Entry;
            return new MyCompanySalaryResponse(
                s.Id, s.CompanyId, x.Slug ?? string.Empty, x.Name, x.Occupation, s.YearsOfExperience,
                s.EmploymentType, s.EmploymentStatus, s.MonthlyNetAmount, s.Currency, s.AnnualBonusAmount,
                s.SubmittedAt, s.UpdatedAt);
        }).ToList();
    }

    /// <summary>A retired row is unknown to a new entry — existing entries keep pointing at it.</summary>
    private async Task EnsureOccupationAsync(Guid occupationId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Occupations.AnyAsync(o => o.Id == occupationId && o.IsActive, cancellationToken))
        {
            throw new CompanySalaryOccupationUnknownException();
        }
    }

    private static SalaryContent ToContent(CompanySalaryRequest request) => new(
        request.OccupationId, request.YearsOfExperience, request.EmploymentType, request.EmploymentStatus,
        request.MonthlyNetAmount, request.Currency, request.HasBonus ? request.AnnualBonusAmount : null);
}
