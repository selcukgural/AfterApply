using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanySalaries;

internal sealed class CompanySalaryService(
    AppDbContext dbContext,
    CompanySlugAllocator slugAllocator,
    HybridCache cache,
    ICompanyCacheInvalidator invalidator,
    IOptions<CompanySalaryOptions> options,
    ContributionNotificationWriter notifications,
    TimeProvider? timeProvider = null)
    : ICompanySalaryService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    // The list is read by every signed-in visitor of the company page and changes only when an
    // entry is written; each write drops the company's tag on every instance, so the TTL is a
    // safety net for a Redis outage, not the staleness bound.
    private static readonly HybridCacheEntryOptions ListCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

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
        var marked = await dbContext.CompanySalaryHelpfulMarks
            .Where(m => m.UserId == userId)
            .Join(dbContext.CompanySalaryEntries.Where(s => s.CompanyId == companyId), m => m.EntryId, s => s.Id, (m, _) => m.EntryId)
            .ToListAsync(cancellationToken);
        return new CompanySalaryViewerStateResponse(own, await GetQuotaAsync(userId, cancellationToken), marked);
    }

    public async Task<CompanySalaryPageResponse?> ListForCompanyAsync(Guid companyId, CompanySalaryListQuery query, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return null;
        }

        // The cache key carries no year: at New Year a company's list may say "current" about a
        // row for up to the TTL, which is the staleness this cache already accepts.
        var windowYears = options.Value.CurrentWindowYears;
        var cutoffYear = SalaryPeriods.CutoffYear(_timeProvider.GetUtcNow().Year, windowYears);

        return await cache.GetOrCreateAsync(CacheKeys.Company.SalaryList(companyId, query.Page), async ct =>
        {
            var entries = dbContext.CompanySalaryEntries.Where(s => s.CompanyId == companyId);
            var total = await entries.CountAsync(ct);
            var pageSize = options.Value.PageSize;

            // SalaryPeriods.IsCurrent, written inline so it translates. A row without a period
            // is not current: nobody knows when it was drawn.
            var current = entries.Where(s => s.PeriodStartYear != null && (s.PeriodEndYear == null || s.PeriodEndYear >= cutoffYear));
            var previousTotal = total - await current.CountAsync(ct);

            // Current rows first, then previous periods by the year they ended, unknown periods
            // last — so the page can draw one "previous periods" line where the current rows run
            // out. Only the columns the public record has a place for: the years and the author
            // are not selected at all. The band is derived in memory — EF cannot translate it,
            // and a page is at most PageSize rows.
            var rows = await entries
                .OrderByDescending(s => s.PeriodStartYear != null && (s.PeriodEndYear == null || s.PeriodEndYear >= cutoffYear))
                .ThenByDescending(s => s.PeriodEndYear ?? (s.PeriodStartYear == null ? 0 : int.MaxValue))
                .ThenByDescending(s => s.SubmittedAt).ThenBy(s => s.Id)
                .Skip((query.Page - 1) * pageSize)
                .Take(pageSize)
                .Join(dbContext.Occupations, s => s.OccupationId, o => o.Id, (s, o) => new
                {
                    s.Id, s.YearsOfExperience, s.EmploymentType, s.EmploymentStatus,
                    s.MonthlyNetAmount, s.Currency, s.AnnualBonusAmount, s.SubmittedAt,
                    s.PeriodStartYear, s.PeriodEndYear,
                    Occupation = new OccupationRefResponse(o.Id, o.Code, o.NameTr, o.NameEn),
                    HelpfulCount = dbContext.CompanySalaryHelpfulMarks.Count(m => m.EntryId == s.Id)
                })
                .ToListAsync(ct);

            var items = rows.Select(s => new CompanySalaryPublicResponse(
                s.Id, s.Occupation, ExperienceBands.From(s.YearsOfExperience), s.EmploymentType, s.EmploymentStatus,
                s.MonthlyNetAmount, s.Currency, s.AnnualBonusAmount,
                // Month precision on purpose — see CompanySalaryPublicResponse.
                s.SubmittedAt.ToString("yyyy-MM"),
                s.PeriodStartYear, s.PeriodEndYear,
                SalaryPeriods.IsCurrent(s.PeriodStartYear, s.PeriodEndYear, cutoffYear),
                s.HelpfulCount)).ToList();

            // The whole company's current amounts, not the page's: a median of page two is not a
            // median, and a median over a 2012 salary is not what the company pays. Bounded by
            // the quota times the number of contributors, so it stays a short list.
            var amounts = await current
                .Select(s => new { s.Currency, s.MonthlyNetAmount })
                .ToListAsync(ct);
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

            return new CompanySalaryPageResponse(items, total, query.Page, pageSize, stats, minimum, previousTotal, windowYears);
        }, ListCacheOptions, tags: [CacheKeys.Company.Tag(companyId)], cancellationToken: cancellationToken);
    }

    public async Task<SalaryPositionResponse?> GetPositionAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        // Filtered on the caller: someone else's entry is "not found", never "forbidden".
        var entry = await dbContext.CompanySalaryEntries
            .Where(s => s.Id == entryId && s.UserId == userId)
            .Join(dbContext.Companies, s => s.CompanyId, c => c.Id, (s, c) => new
            {
                s.CompanyId, s.Currency, s.MonthlyNetAmount, s.PeriodStartYear, s.PeriodEndYear, c.Name, c.Slug
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var settings = options.Value;
        var cutoffYear = SalaryPeriods.CutoffYear(_timeProvider.GetUtcNow().Year, settings.CurrentWindowYears);

        // The same rows the company page's band is made of: current, in this currency, every
        // occupation. The author's own row is one of them when it is current, and the page says so.
        var amounts = await dbContext.CompanySalaryEntries
            .Where(s => s.CompanyId == entry.CompanyId && s.Currency == entry.Currency)
            .Where(s => s.PeriodStartYear != null && (s.PeriodEndYear == null || s.PeriodEndYear >= cutoffYear))
            .Select(s => s.MonthlyNetAmount)
            .ToListAsync(cancellationToken);

        var includesOwn = SalaryPeriods.IsCurrent(entry.PeriodStartYear, entry.PeriodEndYear, cutoffYear);
        var minimum = settings.PersonalBandMinimumEntries;
        decimal? median = null, low = null, high = null;
        int? percent = null;
        if (amounts.Count >= minimum)
        {
            median = SalaryStats.Median(amounts);
            low = amounts.Min();
            high = amounts.Max();
            percent = median > 0
                ? (int)Math.Round((entry.MonthlyNetAmount - median.Value) / median.Value * 100m, MidpointRounding.AwayFromZero)
                : null;
        }

        return new SalaryPositionResponse(entryId, entry.Name, entry.Slug ?? string.Empty, entry.Currency, entry.MonthlyNetAmount,
            amounts.Count, minimum, settings.CurrentWindowYears, includesOwn, median, low, high, percent);
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

        var entry = CompanySalaryEntry.Create(userId, companyId, ToContent(request), _timeProvider.GetUtcNow());
        dbContext.CompanySalaryEntries.Add(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        await invalidator.InvalidateCompanyAsync(companyId, cancellationToken);
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

        entry.Edit(ToContent(request), _timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new CompanySalaryAlreadyExistsException();
        }

        await invalidator.InvalidateCompanyAsync(entry.CompanyId, cancellationToken);
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
        await invalidator.InvalidateCompanyAsync(entry.CompanyId, cancellationToken);
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

    public async Task<HelpfulToggleResponse?> ToggleHelpfulAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        // Salaries are not moderated: every entry is on its company's page, so existing is enough.
        var entry = await dbContext.CompanySalaryEntries
            .Where(s => s.Id == entryId)
            .Select(s => new { s.UserId, s.CompanyId })
            .FirstOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (entry.UserId == userId)
        {
            throw new CompanySalaryNotMarkableException();
        }

        var existing = await dbContext.CompanySalaryHelpfulMarks
            .FirstOrDefaultAsync(m => m.EntryId == entryId && m.UserId == userId, cancellationToken);
        var marked = existing is null;
        if (existing is null)
        {
            dbContext.CompanySalaryHelpfulMarks.Add(CompanySalaryHelpfulMark.Create(entryId, userId, _timeProvider.GetUtcNow()));
        }
        else
        {
            dbContext.CompanySalaryHelpfulMarks.Remove(existing);
        }

        var raced = false;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Double-click: the other request already marked it — and told the author. Same end state.
            marked = true;
            raced = true;
        }

        if (marked && !raced)
        {
            await notifications.RecordHelpfulAsync(ContributionNotificationType.SalaryHelpful, entryId, entry.UserId, userId, cancellationToken);
        }

        // The cached list carries HelpfulCount.
        await invalidator.InvalidateCompanyAsync(entry.CompanyId, cancellationToken);

        var count = await dbContext.CompanySalaryHelpfulMarks.CountAsync(m => m.EntryId == entryId, cancellationToken);
        return new HelpfulToggleResponse(marked, count);
    }

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
                s.SubmittedAt, s.UpdatedAt, s.PeriodStartYear, s.PeriodEndYear);
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

    // The validator has already refused a missing start year; the 0 only exists so a request
    // that skipped validation fails in SalaryContent.Validate rather than storing a null.
    private static SalaryContent ToContent(CompanySalaryRequest request) => new(
        request.OccupationId, request.YearsOfExperience, request.EmploymentType, request.EmploymentStatus,
        request.MonthlyNetAmount, request.Currency, request.HasBonus ? request.AnnualBonusAmount : null,
        request.PeriodStartYear ?? 0, request.PeriodEndYear);
}
