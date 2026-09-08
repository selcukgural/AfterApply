using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies;
using AfterApply.Application.Imports;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Applications;

internal sealed class ApplicationService(
    AppDbContext dbContext, ICompanyResolver companyResolver, IJobResolver jobResolver,
    ICompanySearchService companySearchService, HybridCache cache, IBackgroundJobClient jobClient,
    IOptions<ApplicationBulkOptions> bulkOptions) : IApplicationService
{
    private static readonly HybridCacheEntryOptions SummaryCountsCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(20),
        LocalCacheExpiration = TimeSpan.FromSeconds(20)
    };

    private static string SummaryCountsCacheKey(Guid userId) => $"applications:summary:{userId}";
    /// <summary>
    /// The one place that decides which of a user's applications a search term and a status filter
    /// match. Both the list and every bulk operation go through it, on purpose: a bulk delete
    /// resolved from "the list's current filter" has to hit exactly the rows the list would have
    /// shown, and two hand-written copies of this predicate would eventually stop agreeing about
    /// that.
    ///
    /// It returns applications rather than a joined row so callers that only need the rows (the
    /// bulk paths) get a set-based query with no join at all. The list adds the company join itself
    /// for the name it displays and sorts on. Company name is matched here with EXISTS rather than
    /// through that join, which is equivalent — CompanyId is required — and keeps this usable on its
    /// own.
    /// </summary>
    private IQueryable<DomainApplication> FilteredApplications(Guid userId, string? search, ApplicationStatus? status,
        Guid? companyId = null)
    {
        var applications = dbContext.Applications.Where(a => a.UserId == userId);

        if (status is not null)
        {
            applications = applications.Where(a => a.Status == status);
        }

        if (companyId is not null)
        {
            applications = applications.Where(a => a.CompanyId == companyId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            applications = applications.Where(a => EF.Functions.ILike(a.JobTitle, pattern)
                || dbContext.Companies.Any(c => c.Id == a.CompanyId && EF.Functions.ILike(c.Name, pattern)));
        }

        return applications;
    }

    public async Task<PagedResult<ApplicationSummaryResponse>> GetAllAsync(Guid userId, GetApplicationsQuery query, CancellationToken cancellationToken)
    {
        var joined = FilteredApplications(userId, query.Search, query.Status, query.CompanyId)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a, c.Name });

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
            .Select(x => new ApplicationSummaryResponse(x.a.Id, x.a.CompanyId, x.Name, x.a.JobTitle, x.a.Status, x.a.AppliedAt, x.a.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ApplicationSummaryResponse>(items, totalCount, page, pageSize);
    }

    /// <summary>
    /// How many applications a single company group carries in the response. A cap has to exist
    /// somewhere: without one, a user with 300 applications at one employer would decide the size of
    /// that page for everyone reading it. Anything past the cap is reachable through the flat list
    /// filtered to the company, which is paged.
    /// </summary>
    private const int MaxApplicationsPerGroup = 20;

    public async Task<GroupedApplicationsResponse> GetGroupedByCompanyAsync(Guid userId, GetGroupedApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = FilteredApplications(userId, query.Search, query.Status);

        // Projected to an anonymous type rather than a record of my own, and ordered on its members
        // before any further projection: EF recognises the anonymous type as a transparent identifier
        // and can see through it in ORDER BY, but not a named record (DECISIONS.md 2026-09-08).
        var groups = filtered
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a, c.Name })
            .GroupBy(x => new { x.a.CompanyId, x.Name })
            .Select(g => new
            {
                g.Key.CompanyId,
                g.Key.Name,
                ApplicationCount = g.Count(),
                LastActivityAt = g.Max(x => x.a.UpdatedAt)
            });

        groups = (query.SortBy, query.SortDirection) switch
        {
            (CompanyGroupSortBy.CompanyName, SortDirection.Descending) => groups.OrderByDescending(g => g.Name),
            (CompanyGroupSortBy.CompanyName, _) => groups.OrderBy(g => g.Name),
            (CompanyGroupSortBy.ApplicationCount, SortDirection.Ascending) => groups.OrderBy(g => g.ApplicationCount).ThenBy(g => g.Name),
            (CompanyGroupSortBy.ApplicationCount, _) => groups.OrderByDescending(g => g.ApplicationCount).ThenBy(g => g.Name),
            (_, SortDirection.Ascending) => groups.OrderBy(g => g.LastActivityAt),
            _ => groups.OrderByDescending(g => g.LastActivityAt)
        };

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        // Companies for the pager, applications for "select all N matching" — the two are different
        // numbers and the view shows both.
        var totalCompanyCount = await groups.CountAsync(cancellationToken);
        var totalApplicationCount = await filtered.CountAsync(cancellationToken);

        var pageGroups = await groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageGroups.Count == 0)
        {
            return new GroupedApplicationsResponse([], totalCompanyCount, page, pageSize, totalApplicationCount);
        }

        var companyIds = pageGroups.Select(g => g.CompanyId).ToList();

        // One query for every row belonging to the companies already on this page — never one query
        // per group. Postgres has no per-partition LIMIT that EF can express, so the per-group cap
        // is applied after the rows come back; what that costs is bounded by one user's applications
        // at ten companies. Because nothing is dropped on the way, the status distribution is
        // derived from these same rows rather than asked for separately.
        var rows = await filtered
            .Where(a => companyIds.Contains(a.CompanyId))
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a, c.Name })
            .OrderByDescending(x => x.a.AppliedAt)
            .Select(x => new ApplicationSummaryResponse(x.a.Id, x.a.CompanyId, x.Name, x.a.JobTitle, x.a.Status, x.a.AppliedAt, x.a.UpdatedAt))
            .ToListAsync(cancellationToken);

        var rowsByCompany = rows.GroupBy(r => r.CompanyId).ToDictionary(g => g.Key, g => g.ToList());
        // Biggest segment first, so the bar reads left to right in the order it is drawn.
        var countsByCompany = rowsByCompany.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                .GroupBy(row => row.Status)
                .Select(statusGroup => new CompanyGroupStatusCount(statusGroup.Key, statusGroup.Count()))
                .OrderByDescending(count => count.Count).ThenBy(count => count.Status)
                .ToList());

        var items = pageGroups.Select(group =>
        {
            var companyRows = rowsByCompany.GetValueOrDefault(group.CompanyId, []);
            return new CompanyGroupResponse(
                group.CompanyId,
                group.Name,
                group.ApplicationCount,
                group.LastActivityAt,
                countsByCompany.GetValueOrDefault(group.CompanyId, []),
                companyRows.Take(MaxApplicationsPerGroup).ToList(),
                companyRows.Count > MaxApplicationsPerGroup);
        }).ToList();

        return new GroupedApplicationsResponse(items, totalCompanyCount, page, pageSize, totalApplicationCount);
    }

    public Task<ApplicationSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            SummaryCountsCacheKey(userId),
            userId,
            async (uid, ct) =>
            {
                var counts = await dbContext.Applications
                    .Where(a => a.UserId == uid)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

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
            },
            SummaryCountsCacheOptions,
            cancellationToken: cancellationToken).AsTask();
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
            request.Notes, DateTimeOffset.UtcNow, jobId: null,
            request.HrName, request.HrEmail, request.HrLinkedInUrl,
            await ResolveOwnedCvDocumentIdAsync(userId, request.CvDocumentId, cancellationToken));

        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);

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
        var profileLinks = new CompanyProfileLinks(request.CompanyLinkedInUrl, request.CompanyKariyerNetUrl);

        var companyId = await companySearchService.FindHighConfidenceMatchAsync(request.CompanyName, cancellationToken)
            ?? await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken, profileLinks);

        // Only worth queuing when this submission actually carries a profile URL — a company
        // matched via the trigram/high-confidence path above, or one whose posting linked to
        // neither profile, has nothing new for CompanyEnrichmentService to fetch from. Safe to
        // enqueue immediately: by this point the Company row is already committed, either from an
        // earlier request or by CompanyResolver's own SaveChangesAsync just above — the enqueued
        // job only touches Company, never this method's own not-yet-saved Application.
        if (profileLinks.HasAny)
        {
            jobClient.Enqueue<ICompanyEnrichmentService>(s => s.EnrichAsync(companyId, CancellationToken.None));
        }

        // The job posting's own site (LinkedIn, kariyer.net, ...) tags Job.Source — data
        // provenance — while Source.BrowserExtension always tags how this Application row itself
        // was created, consistent with how Source is used elsewhere (Job.Source = data
        // provenance, Application.Source = entry-creation channel).
        var (jobSource, externalId) = JobPostingSourceResolver.Resolve(normalizedUrl);
        var jobId = await jobResolver.ResolveOrCreateAsync(companyId, request.JobTitle, jobSource, normalizedUrl,
            externalId, request.Location, cancellationToken, request.Description, request.PublishedAt, request.DescriptionHtml);

        // The extension doesn't scrape employment type (spec §11's field list omits it) — same
        // known limitation as generic CSV import (DECISIONS.md Sprint 4), defaults to FullTime.
        var application = DomainApplication.Create(
            userId, companyId, request.JobTitle, normalizedUrl, request.Location,
            EmploymentType.FullTime, DateTimeOffset.UtcNow, Source.BrowserExtension,
            notes: null, DateTimeOffset.UtcNow, jobId,
            request.HrName, request.HrEmail, request.HrLinkedInUrl);

        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);

        return new ExtensionApplicationResponse(await ToDetailAsync(application, cancellationToken), WasDuplicate: false);
    }

    public async Task AttachHrEmailFromIncomingEmailAsync(Guid userId, Guid applicationId, string email, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return;
        }

        application.SetHrEmailFromIncomingEmail(email, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApplicationDetailResponse?> UpdateAsync(Guid userId, Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        application.UpdateDetails(request.JobTitle, request.JobUrl, request.Location,
            request.EmploymentType, request.AppliedAt, request.Notes, DateTimeOffset.UtcNow,
            request.HrName, request.HrEmail, request.HrLinkedInUrl,
            await ResolveOwnedCvDocumentIdAsync(userId, request.CvDocumentId, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(application, cancellationToken);
    }

    /// <summary>
    /// Narrows to exactly the applications a bulk selection covers, always scoped to the caller.
    /// An explicit id list is intersected with the user's own rows rather than trusted — an id in a
    /// request body is a claim, never proof of ownership — so a foreign id simply matches nothing
    /// instead of leaking whether it exists.
    /// </summary>
    private IQueryable<DomainApplication> ResolveSelection(Guid userId, BulkSelection selection)
    {
        if (selection.Ids is { Count: > 0 } ids)
        {
            return dbContext.Applications.Where(a => a.UserId == userId && ids.Contains(a.Id));
        }

        var filter = selection.AllMatching ?? new BulkFilterSelection();
        return FilteredApplications(userId, filter.Search, filter.Status, filter.CompanyId);
    }

    /// <summary>
    /// Refuses the operation when the number of matching rows is not what the user was shown. Only
    /// meaningful for a filter-resolved selection: an explicit id list is already the exact set the
    /// user ticked, and comparing it against itself would only reject legitimate requests whose rows
    /// were deleted in another tab.
    /// </summary>
    private static async Task GuardExpectedCountAsync(IQueryable<DomainApplication> selected, BulkSelection selection,
        int? expectedCount, CancellationToken cancellationToken)
    {
        if (selection.AllMatching is null || expectedCount is null)
        {
            return;
        }

        var actualCount = await selected.CountAsync(cancellationToken);
        if (actualCount != expectedCount.Value)
        {
            throw new BulkCountMismatchException(expectedCount.Value, actualCount);
        }
    }

    public async Task<BulkChangeStatusResponse> BulkChangeStatusAsync(Guid userId, BulkChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var selected = ResolveSelection(userId, request.Selection);
        await GuardExpectedCountAsync(selected, request.Selection, request.ExpectedCount, cancellationToken);

        var maxOperationSize = bulkOptions.Value.MaxOperationSize;
        // Take one past the ceiling so the overflow is detectable without a second COUNT query.
        var applications = await selected.Take(maxOperationSize + 1).ToListAsync(cancellationToken);
        if (applications.Count > maxOperationSize)
        {
            throw new BulkOperationTooLargeException(maxOperationSize);
        }

        // One timestamp for the whole batch: these rows changed in a single act, and stamping each
        // with its own DateTimeOffset.UtcNow would scatter them across the history for no reason.
        var changedAt = DateTimeOffset.UtcNow;
        var context = new StatusChangeContext(Source.Manual, StatusChangeOrigin.BulkEdit, request.Note);
        var changes = new List<BulkStatusChange>(applications.Count);
        var skipped = 0;

        foreach (var application in applications)
        {
            // Already-in-status is the expected case in a bulk selection, not an error: the user
            // rubber-banded a range and some of it was already where they are sending it.
            if (application.Status == request.NewStatus)
            {
                skipped++;
                continue;
            }

            var fromStatus = application.Status;
            application.ChangeStatus(request.NewStatus, changedAt, context);

            // Added explicitly rather than left to change tracking, for the reason spelled out in
            // ChangeStatusAsync: the collections were never Included, so EF has no prior snapshot.
            dbContext.ApplicationStatusHistories.Add(application.StatusHistory.Last());
            dbContext.ApplicationEvents.Add(application.Events.Last());

            changes.Add(new BulkStatusChange(application.Id, fromStatus, request.NewStatus));
        }

        if (changes.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);
        }

        return new BulkChangeStatusResponse(changes.Count, skipped, changes);
    }

    public async Task<UndoBulkStatusResponse> UndoBulkStatusAsync(Guid userId, UndoBulkStatusRequest request,
        CancellationToken cancellationToken)
    {
        var maxOperationSize = bulkOptions.Value.MaxOperationSize;
        if (request.Entries.Count > maxOperationSize)
        {
            throw new BulkOperationTooLargeException(maxOperationSize);
        }

        var entriesById = request.Entries
            .GroupBy(e => e.ApplicationId)
            .ToDictionary(g => g.Key, g => g.First());

        var ids = entriesById.Keys.ToList();
        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId && ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var changedAt = DateTimeOffset.UtcNow;
        var context = new StatusChangeContext(Source.Manual, StatusChangeOrigin.BulkEditReverted);
        var reverted = 0;

        foreach (var application in applications)
        {
            var entry = entriesById[application.Id];

            // Compare-and-set on the status the client last saw. Anything that moved on since — the
            // user corrected one row by hand, or an email suggestion landed — keeps the newer
            // decision: an undo puts back what this operation did, and nothing else.
            if (application.Status != entry.ExpectedStatus || application.Status == entry.RevertTo)
            {
                continue;
            }

            application.ChangeStatus(entry.RevertTo, changedAt, context);
            dbContext.ApplicationStatusHistories.Add(application.StatusHistory.Last());
            dbContext.ApplicationEvents.Add(application.Events.Last());
            reverted++;
        }

        if (reverted > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);
        }

        return new UndoBulkStatusResponse(reverted, request.Entries.Count - reverted);
    }

    public async Task<BulkDeleteResponse> BulkDeleteAsync(Guid userId, BulkDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var selected = ResolveSelection(userId, request.Selection);
        await GuardExpectedCountAsync(selected, request.Selection, request.ExpectedCount, cancellationToken);

        // Set-based, and deliberately not capped by MaxOperationSize: no entities are loaded, and
        // the whole point of "Delete All" is an account whose row count may well exceed that ceiling.
        // Everything hanging off an application (events, status history, reminders, matched email
        // suggestions) goes with it through the FKs' ON DELETE CASCADE — see ApplicationConfiguration
        // and DECISIONS.md 2026-09-07 on why none of it is recoverable afterwards.
        //
        // The count guard above and this delete are two statements, so a row created between them
        // would slip in. That window is a single user racing themselves across two tabs, and closing
        // it would cost a serializable transaction on every delete; the guard is here for the far
        // likelier case of a screen that has simply gone stale.
        var deleted = await selected.ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);
        }

        return new BulkDeleteResponse(deleted);
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
        await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);
        return true;
    }

    public Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        // The HTTP surface can only ever produce a manual change. Origin is not taken from the
        // request body on purpose — a status history that a client can label however it likes is
        // not a history.
        return ChangeStatusAsync(userId, applicationId, request.NewStatus,
            request.ChangedAt ?? DateTimeOffset.UtcNow, StatusChangeContext.Manual(request.Note), cancellationToken);
    }

    public async Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId,
        ApplicationStatus newStatus, DateTimeOffset changedAt, StatusChangeContext context, CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(userId, applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        application.ChangeStatus(newStatus, changedAt, context);

        // application.Events/StatusHistory were never Included (FindOwnedAsync
        // loads the bare row), so EF has no prior tracking entry to confuse the new
        // items with — explicitly Add()-ing them, rather than relying on EF to
        // detect the mutation of an Included collection, sidesteps a real EF Core
        // issue where DetectChanges can mis-snapshot newly-added items in a loaded
        // collection navigation as Modified (UPDATE) instead of Added (INSERT).
        dbContext.ApplicationStatusHistories.Add(application.StatusHistory.Last());
        dbContext.ApplicationEvents.Add(application.Events.Last());

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(SummaryCountsCacheKey(userId), cancellationToken);

        return await ToDetailAsync(application, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ApplicationStatusHistoryResponse>?> GetStatusHistoryAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var owned = await dbContext.Applications.AnyAsync(a => a.Id == applicationId && a.UserId == userId, cancellationToken);
        if (!owned)
        {
            return null;
        }

        // Left join rather than a required relationship: EmailSuggestionId is a soft link, and a
        // history row has to keep reading correctly after its suggestion is gone.
        return await dbContext.ApplicationStatusHistories
            .Where(h => h.ApplicationId == applicationId)
            // Two rows can share a ChangedAt — an import stamps both the seed row and the status it
            // carries with the CSV's applied date. Id cannot break that tie: Guid v7 is only ordered
            // down to the millisecond, and within one millisecond its low bits are random. The seed
            // row is the one with no FromStatus, and it is by definition the earliest, so in a
            // newest-first list it sorts last among equals. Id is a final key only so the remainder
            // is stable across queries rather than left to the plan.
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.FromStatus != null)
            .ThenByDescending(h => h.Id)
            .Select(h => new ApplicationStatusHistoryResponse(h.Id, h.FromStatus, h.ToStatus, h.ChangedAt,
                h.Note, h.Origin, h.Source, h.EmailSuggestionId, h.RejectionReasonCategory, h.RejectionReasonDetail,
                dbContext.EmailSuggestions
                    .Where(s => s.Id == h.EmailSuggestionId)
                    .Select(s => s.Subject)
                    .FirstOrDefault(),
                dbContext.EmailSuggestions
                    .Where(s => s.Id == h.EmailSuggestionId)
                    .Select(s => s.Snippet)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
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

    /// <summary>
    /// Passes a CV id through only when it names one of this user's own CVs, and drops it to null
    /// otherwise. A body field is not evidence of ownership: without this check, anyone could point
    /// their application at someone else's CV row and read the file name straight back out of the
    /// detail response. Silently null rather than an error — the only ways to get here with an id
    /// the user does not own are a stale form (the CV was deleted between load and save) and an
    /// attempt, and neither deserves to fail the save of everything else on the form.
    /// </summary>
    private async Task<Guid?> ResolveOwnedCvDocumentIdAsync(Guid userId, Guid? cvDocumentId,
        CancellationToken cancellationToken)
    {
        if (cvDocumentId is not { } id)
        {
            return null;
        }

        var owned = await dbContext.CvDocuments
            .AnyAsync(d => d.Id == id && d.UserId == userId, cancellationToken);

        return owned ? id : null;
    }

    private Task<DomainApplication?> FindOwnedAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, cancellationToken);
    }

    private async Task<ApplicationDetailResponse> ToDetailAsync(DomainApplication application, CancellationToken cancellationToken)
    {
        // One projection for every company field the detail view shows — all of them are read here
        // rather than copied onto the Application because CompanyEnrichmentService fills Website,
        // Industry and Country in the background, after this row already exists.
        var company = await dbContext.Companies
            .Where(c => c.Id == application.CompanyId)
            .Select(c => new { c.Name, c.Website, c.LinkedInUrl, c.KariyerNetUrl, c.Industry, c.Country })
            .FirstAsync(cancellationToken);

        var jobDescriptionHtml = application.JobId is null
            ? null
            : await dbContext.Jobs
                .Where(j => j.Id == application.JobId)
                .Select(j => j.DescriptionHtml)
                .FirstOrDefaultAsync(cancellationToken);

        // Scoped to the owner as well as to the id. The stored id is already ownership-checked on
        // the way in, but a read that only matched on id would silently start leaking file names
        // the moment anything else ever wrote this column.
        var cvDocumentFileName = application.CvDocumentId is null
            ? null
            : await dbContext.CvDocuments
                .Where(d => d.Id == application.CvDocumentId && d.UserId == application.UserId)
                .Select(d => d.FileName)
                .FirstOrDefaultAsync(cancellationToken);

        return new ApplicationDetailResponse(
            application.Id, application.CompanyId, company.Name, company.Website, company.LinkedInUrl,
            application.JobTitle, application.JobUrl, application.Location, application.EmploymentType,
            application.AppliedAt, application.Status, application.Source, application.Notes,
            application.CreatedAt, application.UpdatedAt, jobDescriptionHtml,
            application.HrName, application.HrEmail, application.HrLinkedInUrl, application.HrEmailSource,
            application.CvDocumentId, cvDocumentFileName,
            company.KariyerNetUrl, company.Industry, company.Country);
    }
}
