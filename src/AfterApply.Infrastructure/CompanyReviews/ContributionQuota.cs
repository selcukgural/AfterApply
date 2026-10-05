using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.CompanyReviews;

/// <summary>The one per-account limit an admin sets for reviews, salaries and candidate
/// experiences alike (<c>Users.ContributionQuotaOverride</c>); each kind's quota reads it here and
/// falls back to its own configured default.</summary>
internal static class ContributionQuota
{
    public static Task<int?> OverrideAsync(this AppDbContext dbContext, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ContributionQuotaOverride)
            .FirstOrDefaultAsync(cancellationToken);
}
