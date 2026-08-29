using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.TrackedJobs;

internal sealed class TrackedJobService(
    AppDbContext dbContext, ICompanyResolver companyResolver, HybridCache cache) : ITrackedJobService
{
    private static string ApplicationsSummaryCacheKey(Guid userId) => $"applications:summary:{userId}";

    public async Task<IReadOnlyCollection<TrackedJobResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.TrackedJobs
            .Where(t => t.UserId == userId)
            .Join(dbContext.Companies, t => t.CompanyId, c => c.Id, (t, c) => new { t, c.Name })
            .OrderByDescending(x => x.t.AddedAt)
            .Select(x => new TrackedJobResponse(
                x.t.Id, x.t.CompanyId, x.Name, x.t.JobTitle, x.t.JobUrl, x.t.Location, x.t.Notes, x.t.AddedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TrackedJobResponse> CreateAsync(Guid userId, CreateTrackedJobRequest request, CancellationToken cancellationToken)
    {
        var companyId = await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var trackedJob = TrackedJob.Create(userId, companyId, request.JobTitle, request.JobUrl, request.Location, request.Notes, now);

        dbContext.TrackedJobs.Add(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TrackedJobResponse(
            trackedJob.Id, trackedJob.CompanyId, request.CompanyName, trackedJob.JobTitle,
            trackedJob.JobUrl, trackedJob.Location, trackedJob.Notes, trackedJob.AddedAt);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid trackedJobId, CancellationToken cancellationToken)
    {
        var trackedJob = await FindOwnedAsync(userId, trackedJobId, cancellationToken);
        if (trackedJob is null)
        {
            return false;
        }

        dbContext.TrackedJobs.Remove(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ApplicationDetailResponse?> ConvertToApplicationAsync(Guid userId, Guid trackedJobId,
        ConvertTrackedJobRequest request, CancellationToken cancellationToken)
    {
        var trackedJob = await FindOwnedAsync(userId, trackedJobId, cancellationToken);
        if (trackedJob is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var application = DomainApplication.Create(
            userId, trackedJob.CompanyId, trackedJob.JobTitle, trackedJob.JobUrl, trackedJob.Location,
            request.EmploymentType, request.AppliedAt, Source.Manual, request.Notes ?? trackedJob.Notes, now);

        dbContext.Applications.Add(application);
        dbContext.TrackedJobs.Remove(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ApplicationsSummaryCacheKey(userId), cancellationToken);

        var companyName = await dbContext.Companies
            .Where(c => c.Id == application.CompanyId)
            .Select(c => c.Name)
            .FirstAsync(cancellationToken);

        return new ApplicationDetailResponse(
            application.Id, application.CompanyId, companyName, application.JobTitle, application.JobUrl,
            application.Location, application.EmploymentType, application.AppliedAt, application.Status,
            application.Source, application.Notes, application.CreatedAt, application.UpdatedAt);
    }

    private Task<TrackedJob?> FindOwnedAsync(Guid userId, Guid trackedJobId, CancellationToken cancellationToken)
    {
        return dbContext.TrackedJobs.FirstOrDefaultAsync(t => t.Id == trackedJobId && t.UserId == userId, cancellationToken);
    }
}
