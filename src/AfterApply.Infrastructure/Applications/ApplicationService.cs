using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies;
using AfterApply.Application.Imports;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Applications;

internal sealed class ApplicationService(
    AppDbContext dbContext, ICompanyResolver companyResolver, IJobResolver jobResolver, ICompanySearchService companySearchService) : IApplicationService
{
    public async Task<PagedResult<ApplicationSummaryResponse>> GetAllAsync(Guid userId, GetApplicationsQuery query, CancellationToken cancellationToken)
    {
        var joined = dbContext.Applications
            .Where(a => a.UserId == userId)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a, c.Name });

        if (query.Status is not null)
        {
            joined = joined.Where(x => x.a.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            joined = joined.Where(x => EF.Functions.ILike(x.Name, pattern) || EF.Functions.ILike(x.a.JobTitle, pattern));
        }

        joined = (query.SortBy, query.SortDirection) switch
        {
            (ApplicationListSortBy.CompanyName, SortDirection.Ascending) => joined.OrderBy(x => x.Name),
            (ApplicationListSortBy.CompanyName, SortDirection.Descending) => joined.OrderByDescending(x => x.Name),
            (ApplicationListSortBy.JobTitle, SortDirection.Ascending) => joined.OrderBy(x => x.a.JobTitle),
            (ApplicationListSortBy.JobTitle, SortDirection.Descending) => joined.OrderByDescending(x => x.a.JobTitle),
            (ApplicationListSortBy.Status, SortDirection.Ascending) => joined.OrderBy(x => x.a.Status),
            (ApplicationListSortBy.Status, SortDirection.Descending) => joined.OrderByDescending(x => x.a.Status),
            (ApplicationListSortBy.UpdatedAt, SortDirection.Ascending) => joined.OrderBy(x => x.a.UpdatedAt),
            (ApplicationListSortBy.UpdatedAt, SortDirection.Descending) => joined.OrderByDescending(x => x.a.UpdatedAt),
            (_, SortDirection.Ascending) => joined.OrderBy(x => x.a.AppliedAt),
            _ => joined.OrderByDescending(x => x.a.AppliedAt)
        };

        var totalCount = await joined.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await joined
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ApplicationSummaryResponse(x.a.Id, x.Name, x.a.JobTitle, x.a.Status, x.a.AppliedAt, x.a.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ApplicationSummaryResponse>(items, totalCount, page, pageSize);
    }

    public async Task<ApplicationSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var counts = await dbContext.Applications
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        int Get(ApplicationStatus status) => counts.GetValueOrDefault(status);

        var active = Get(ApplicationStatus.Applied) + Get(ApplicationStatus.Screening)
            + Get(ApplicationStatus.Interview) + Get(ApplicationStatus.TechnicalInterview) + Get(ApplicationStatus.FinalInterview);
        var interviews = Get(ApplicationStatus.Interview) + Get(ApplicationStatus.TechnicalInterview) + Get(ApplicationStatus.FinalInterview);

        return new ApplicationSummaryCountsResponse(
            Total: counts.Values.Sum(),
            Active: active,
            Waiting: Get(ApplicationStatus.Offer),
            Interviews: interviews,
            Offers: Get(ApplicationStatus.Offer),
            Rejected: Get(ApplicationStatus.Rejected),
            Ghosted: Get(ApplicationStatus.Ghosted));
    }

    public async Task<ApplicationDetailResponse?> GetByIdAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        return application is null ? null : await ToDetailAsync(application, cancellationToken);
    }

    public async Task<ApplicationDetailResponse> CreateAsync(Guid userId, CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var companyId = await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken);

        var application = DomainApplication.Create(
            userId, companyId, request.JobTitle, request.JobUrl, request.Location,
            request.EmploymentType, request.AppliedAt, request.Source ?? Source.Manual,
            request.Notes, DateTimeOffset.UtcNow);

        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<ExtensionApplicationResponse> CreateFromExtensionAsync(Guid userId, CreateFromExtensionRequest request, CancellationToken cancellationToken)
    {
        var normalizedUrl = request.JobUrl.Trim();

        var existing = await dbContext.Applications
            .FirstOrDefaultAsync(a => a.UserId == userId && a.JobUrl == normalizedUrl, cancellationToken);

        if (existing is not null)
        {
            return new ExtensionApplicationResponse(await ToDetailAsync(existing, cancellationToken), WasDuplicate: true);
        }

        // Scraped names are often near-duplicates of an existing Company (typos, "Corp" vs
        // "Corporation") rather than a genuinely new one — a high-confidence trigram match is
        // silently attached to first, falling back to the unchanged exact-match-or-create
        // resolver only when no such match exists. Manual entry (CreateAsync) is unaffected: it
        // still calls ResolveOrCreateAsync directly, since the autocomplete UI already steers
        // users to type an existing company's exact name when one applies.
        var companyId = await companySearchService.FindHighConfidenceMatchAsync(request.CompanyName, cancellationToken)
            ?? await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken);

        // The job posting was scraped from a LinkedIn page (Source.LinkedIn — reserved for this
        // exact use since Sprint 5, see DECISIONS.md); Source.BrowserExtension instead tags how
        // this Application row itself was created, consistent with how Source is used elsewhere
        // (Job.Source = data provenance, Application.Source = entry-creation channel).
        var externalId = LinkedInJobIdExtractor.Extract(normalizedUrl);
        var jobId = await jobResolver.ResolveOrCreateAsync(companyId, request.JobTitle, Source.LinkedIn, normalizedUrl,
            externalId, request.Location, cancellationToken, request.Description, request.PublishedAt, request.DescriptionHtml);

        // The extension doesn't scrape employment type (spec §11's field list omits it) — same
        // known limitation as generic CSV import (DECISIONS.md Sprint 4), defaults to FullTime.
        var application = DomainApplication.Create(
            userId, companyId, request.JobTitle, normalizedUrl, request.Location,
            EmploymentType.FullTime, DateTimeOffset.UtcNow, Source.BrowserExtension,
            notes: null, DateTimeOffset.UtcNow, jobId);

        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExtensionApplicationResponse(await ToDetailAsync(application, cancellationToken), WasDuplicate: false);
    }

    public async Task<ApplicationDetailResponse?> UpdateAsync(Guid userId, Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        application.UpdateDetails(request.JobTitle, request.JobUrl, request.Location,
            request.EmploymentType, request.AppliedAt, request.Notes, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        dbContext.Applications.Remove(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        application.ChangeStatus(request.NewStatus, request.ChangedAt ?? DateTimeOffset.UtcNow, request.Source ?? Source.Manual, request.Note);

        // application.Events/StatusHistory were never Included (FindOwnedAsync
        // loads the bare row), so EF has no prior tracking entry to confuse the new
        // items with — explicitly Add()-ing them, rather than relying on EF to
        // detect the mutation of an Included collection, sidesteps a real EF Core
        // issue where DetectChanges can mis-snapshot newly-added items in a loaded
        // collection navigation as Modified (UPDATE) instead of Added (INSERT).
        dbContext.ApplicationStatusHistories.Add(application.StatusHistory.Last());
        dbContext.ApplicationEvents.Add(application.Events.Last());

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ApplicationEventResponse>?> GetTimelineAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var owned = await dbContext.Applications.AnyAsync(a => a.Id == applicationId && a.UserId == userId, cancellationToken);
        if (!owned)
        {
            return null;
        }

        return await dbContext.ApplicationEvents
            .Where(e => e.ApplicationId == applicationId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new ApplicationEventResponse(e.Id, e.Type, e.OccurredAt, e.Source, e.Metadata))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationEventResponse?> AddEventAsync(Guid userId, Guid applicationId, CreateEventRequest request, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        application.AddEvent(request.Type, request.OccurredAt ?? DateTimeOffset.UtcNow, request.Source ?? Source.Manual, request.Metadata);

        var addedEvent = application.Events.Last();
        dbContext.ApplicationEvents.Add(addedEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApplicationEventResponse(addedEvent.Id, addedEvent.Type, addedEvent.OccurredAt, addedEvent.Source, addedEvent.Metadata);
    }

    private Task<DomainApplication?> FindOwnedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, cancellationToken);
    }

    private async Task<ApplicationDetailResponse> ToDetailAsync(DomainApplication application, CancellationToken cancellationToken)
    {
        var companyName = await dbContext.Companies
            .Where(c => c.Id == application.CompanyId)
            .Select(c => c.Name)
            .FirstAsync(cancellationToken);

        var job = application.JobId is null
            ? null
            : await dbContext.Jobs
                .Where(j => j.Id == application.JobId)
                .Select(j => new { j.Description, j.DescriptionHtml })
                .FirstOrDefaultAsync(cancellationToken);

        return new ApplicationDetailResponse(
            application.Id, application.CompanyId, companyName, application.JobTitle, application.JobUrl,
            application.Location, application.EmploymentType, application.AppliedAt, application.Status,
            application.Source, application.Notes, application.CreatedAt, application.UpdatedAt,
            job?.Description, job?.DescriptionHtml);
    }
}
