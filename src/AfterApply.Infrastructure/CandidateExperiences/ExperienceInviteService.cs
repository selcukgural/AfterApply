using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CandidateExperiences;

internal sealed class ExperienceInviteService(
    AppDbContext dbContext,
    CompanySlugAllocator slugAllocator,
    IOptions<CandidateExperienceOptions> options,
    TimeProvider? timeProvider = null)
    : IExperienceInviteService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>The closings a process can be rated after: the company said no, went quiet, or
    /// hired. A withdrawal is left out, as the application page's closing invite leaves it out —
    /// it was the candidate's own call, and asking them to grade the company for it reads as
    /// a reproach.</summary>
    private static readonly ApplicationStatus[] InviteStatuses =
        [ApplicationStatus.Rejected, ApplicationStatus.Ghosted, ApplicationStatus.Accepted];

    public async Task<IReadOnlyList<ExperienceInviteResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = _timeProvider.GetUtcNow();
        var endedBefore = now.AddDays(-settings.InviteDelayDays);
        var endedAfter = now.AddDays(-settings.InviteMaxAgeDays);

        // When a process ended is the moment it moved into the status it holds now — the latest
        // history row pointing there. Companies already rated, or waved away, are out: an
        // experience is one per company, so one "no thanks" covers every process there.
        var ended = await dbContext.Applications
            .Where(a => a.UserId == userId && InviteStatuses.Contains(a.Status))
            .Where(a => !dbContext.CandidateExperiences.Any(e => e.UserId == userId && e.CompanyId == a.CompanyId))
            .Where(a => !dbContext.ExperienceInviteDismissals.Any(d => d.UserId == userId && d.CompanyId == a.CompanyId))
            .Select(a => new
            {
                a.Id,
                a.CompanyId,
                a.JobTitle,
                a.Status,
                EndedAt = dbContext.ApplicationStatusHistories
                    .Where(h => h.ApplicationId == a.Id && h.ToStatus == a.Status)
                    .Max(h => (DateTimeOffset?)h.ChangedAt)
            })
            .Where(x => x.EndedAt != null && x.EndedAt <= endedBefore && x.EndedAt >= endedAfter)
            .ToListAsync(cancellationToken);

        // One line per company — its most recent closing — newest first.
        var picked = ended
            .GroupBy(x => x.CompanyId)
            .Select(g => g.OrderByDescending(x => x.EndedAt).First())
            .OrderByDescending(x => x.EndedAt)
            .Take(settings.InviteLimit)
            .ToList();
        if (picked.Count == 0)
        {
            return [];
        }

        var companyIds = picked.Select(x => x.CompanyId).ToList();
        var companies = await dbContext.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var result = new List<ExperienceInviteResponse>(picked.Count);
        foreach (var row in picked)
        {
            var company = companies[row.CompanyId];
            // The invite links to the company's rating form by slug; a company that predates slugs
            // gets its slug here, the way the viewer-state read does.
            await slugAllocator.EnsureSlugAsync(company, cancellationToken);
            result.Add(new ExperienceInviteResponse(row.Id, company.Id, company.Name, company.Slug!, row.JobTitle, row.Status, row.EndedAt!.Value));
        }

        return result;
    }

    public async Task<bool> DismissAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        // Only a company the caller has applied to can be dismissed — any other id is "not found",
        // never a way to learn which companies exist.
        if (!await dbContext.Applications.AnyAsync(a => a.UserId == userId && a.CompanyId == companyId, cancellationToken))
        {
            return false;
        }

        if (await dbContext.ExperienceInviteDismissals.AnyAsync(d => d.UserId == userId && d.CompanyId == companyId, cancellationToken))
        {
            return true;
        }

        dbContext.ExperienceInviteDismissals.Add(ExperienceInviteDismissal.Create(userId, companyId, _timeProvider.GetUtcNow()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Two tabs dismissed the same company at once: the other request's row is the answer.
        }

        return true;
    }
}
