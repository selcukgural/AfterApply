using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>
/// The "backed by a tracked application" label on a contribution (2026-09-24, research item #7).
/// A contribution carries it when its author had an application at the same company in
/// e-kariyerim at least <see cref="LeadTime"/> before the contribution was first written; a
/// salary or a review needs that application to have ended <see cref="ApplicationStatus.Accepted"/>,
/// because having applied does not show someone worked there.
///
/// Measured on the server-stamped <c>CreatedAt</c> of both rows and nothing else: the applied
/// date, the source and the status-change dates all come from the client and can be backdated,
/// so adding an application the day before writing a review does not earn the label. The label
/// says nothing about the content — the terms still say contributions are not verified — and the
/// wire carries only the yes/no, never which application or when.
///
/// Computed on read, not stored: deleting the application takes the label with it.
/// </summary>
internal sealed class ContributionProofQueries(AppDbContext dbContext)
{
    /// <summary>The web copy says "14 gün / 14 days" — change both together.</summary>
    public static readonly TimeSpan LeadTime = TimeSpan.FromDays(14);

    public async Task<HashSet<Guid>> BackedExperienceIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var lead = LeadTime;
        return (await dbContext.CandidateExperiences
                .Where(e => ids.Contains(e.Id))
                .Where(e => dbContext.Applications.Any(a =>
                    a.UserId == e.UserId && a.CompanyId == e.CompanyId && a.CreatedAt <= e.CreatedAt - lead))
                .Select(e => e.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    public async Task<HashSet<Guid>> BackedSalaryIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var lead = LeadTime;
        return (await dbContext.CompanySalaryEntries
                .Where(s => ids.Contains(s.Id))
                .Where(s => dbContext.Applications.Any(a =>
                    a.UserId == s.UserId && a.CompanyId == s.CompanyId && a.Status == ApplicationStatus.Accepted
                    && a.CreatedAt <= s.CreatedAt - lead))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    public async Task<HashSet<Guid>> BackedReviewIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var lead = LeadTime;
        return (await dbContext.CompanyReviews
                .Where(r => ids.Contains(r.Id))
                .Where(r => dbContext.Applications.Any(a =>
                    a.UserId == r.UserId && a.CompanyId == r.CompanyId && a.Status == ApplicationStatus.Accepted
                    && a.CreatedAt <= r.CreatedAt - lead))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
