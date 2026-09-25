using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Applications;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.TrackedJobs;

internal sealed class TrackedJobService(
    AppDbContext dbContext, ICompanyResolver companyResolver, ExtensionCaptureResolver captureResolver,
    HybridCache cache) : ITrackedJobService
{
    public async Task<IReadOnlyCollection<TrackedJobResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.TrackedJobs
            .Where(t => t.UserId == userId)
            .Join(dbContext.Companies, t => t.CompanyId, c => c.Id, (t, c) => new { t, c })
            .OrderByDescending(x => x.t.AddedAt)
            .Select(x => new TrackedJobResponse(
                x.t.Id, x.t.CompanyId, x.c.Name, x.c.Website, x.c.LinkedInUrl,
                x.t.JobTitle, x.t.JobUrl, x.t.Location, x.t.Notes, x.t.AddedAt,
                x.t.HrName, x.t.HrEmail, x.t.HrLinkedInUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task<TrackedJobResponse> CreateAsync(Guid userId, CreateTrackedJobRequest request, CancellationToken cancellationToken)
    {
        var companyId = await companyResolver.ResolveOrCreateAsync(request.CompanyName, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var trackedJob = TrackedJob.Create(userId, companyId, request.JobTitle, request.JobUrl,
            request.Location, request.Notes, now, request.HrName, request.HrEmail, request.HrLinkedInUrl);

        dbContext.TrackedJobs.Add(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Read back the resolved row rather than echoing request.CompanyName: the resolver matches
        // on the normalized name, so a request for "acme corp" can attach to an existing "Acme
        // Corp" — and the response should say which company it actually landed on, with whatever
        // links that company already has.
        var company = await ReadCompanyAsync(companyId, cancellationToken);

        return new TrackedJobResponse(
            trackedJob.Id, trackedJob.CompanyId, company.Name, company.Website, company.LinkedInUrl,
            trackedJob.JobTitle, trackedJob.JobUrl, trackedJob.Location, trackedJob.Notes, trackedJob.AddedAt,
            trackedJob.HrName, trackedJob.HrEmail, trackedJob.HrLinkedInUrl);
    }

    public async Task<ExtensionTrackedJobResponse> CreateFromExtensionAsync(Guid userId, CreateFromExtensionRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUrl = request.JobUrl.Trim();

        if (await dbContext.Applications.AnyAsync(a => a.UserId == userId && a.JobUrl == normalizedUrl, cancellationToken))
        {
            return new ExtensionTrackedJobResponse(ExtensionTrackedJobOutcome.AlreadyApplied);
        }

        if (await dbContext.TrackedJobs.AnyAsync(t => t.UserId == userId && t.JobUrl == normalizedUrl, cancellationToken))
        {
            return new ExtensionTrackedJobResponse(ExtensionTrackedJobOutcome.AlreadySaved);
        }

        var (companyId, jobId) = await captureResolver.ResolveAsync(userId, request, normalizedUrl, cancellationToken);

        var trackedJob = TrackedJob.Create(userId, companyId, request.JobTitle, normalizedUrl, request.Location,
            notes: null, DateTimeOffset.UtcNow, request.HrName, request.HrEmail, request.HrLinkedInUrl,
            jobId, request.DescriptionHtml);

        dbContext.TrackedJobs.Add(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ExtensionTrackedJobResponse(ExtensionTrackedJobOutcome.Saved);
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
        // The HR contact travels with the job: a user who tracked a posting and recorded who to
        // talk to should not have to retype it the moment they actually apply.
        var application = DomainApplication.Create(
            userId, trackedJob.CompanyId, trackedJob.JobTitle, trackedJob.JobUrl, trackedJob.Location,
            request.EmploymentType, request.AppliedAt, Source.Manual, request.Notes ?? trackedJob.Notes, now,
            trackedJob.JobId, trackedJob.HrName, trackedJob.HrEmail, trackedJob.HrLinkedInUrl,
            capturedJobDescriptionHtml: trackedJob.CapturedJobDescriptionHtml);

        dbContext.Applications.Add(application);
        dbContext.TrackedJobs.Remove(trackedJob);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.ApplicationsSummary(userId), cancellationToken);

        var company = await ReadCompanyAsync(application.CompanyId, cancellationToken);

        return new ApplicationDetailResponse(
            application.Id, application.CompanyId, company.Name, company.Website, company.LinkedInUrl,
            application.JobTitle, application.JobUrl, application.Location, application.EmploymentType,
            application.AppliedAt, application.Status, application.Source, application.Notes,
            application.CreatedAt, application.UpdatedAt, JobDescriptionHtml: application.CapturedJobDescriptionHtml,
            application.HrName, application.HrEmail, application.HrLinkedInUrl);
    }

    private Task<TrackedJob?> FindOwnedAsync(Guid userId, Guid trackedJobId, CancellationToken cancellationToken)
    {
        return dbContext.TrackedJobs.FirstOrDefaultAsync(t => t.Id == trackedJobId && t.UserId == userId, cancellationToken);
    }

    private Task<CompanyLinks> ReadCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyLinks(c.Name, c.Website, c.LinkedInUrl))
            .FirstAsync(cancellationToken);
    }

    private sealed record CompanyLinks(string Name, string? Website, string? LinkedInUrl);
}
