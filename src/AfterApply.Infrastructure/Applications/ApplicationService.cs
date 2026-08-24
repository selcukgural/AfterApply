using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Applications;

internal sealed class ApplicationService(AppDbContext dbContext, ICompanyResolver companyResolver) : IApplicationService
{
    public async Task<IReadOnlyCollection<ApplicationSummaryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Applications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AppliedAt)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id,
                (a, c) => new ApplicationSummaryResponse(a.Id, c.Name, a.JobTitle, a.Status, a.AppliedAt, a.UpdatedAt))
            .ToListAsync(cancellationToken);
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

        application.ChangeStatus(request.NewStatus, request.ChangedAt ?? DateTimeOffset.UtcNow, Source.Manual, request.Note);

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

        return new ApplicationDetailResponse(
            application.Id, application.CompanyId, companyName, application.JobTitle, application.JobUrl,
            application.Location, application.EmploymentType, application.AppliedAt, application.Status,
            application.Source, application.Notes, application.CreatedAt, application.UpdatedAt);
    }
}
