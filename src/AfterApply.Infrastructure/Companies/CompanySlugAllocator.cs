using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Companies;

/// <summary>
/// Picks the first free slug for a company name: the generator's base, else <c>base-2</c>,
/// <c>base-3</c>, … Reads the table once (every slug that starts with the base) and decides in
/// memory. Two requests creating the same name at the same instant can still both choose the same
/// suffix; the unique index catches that and the caller retries with the next one.
/// </summary>
internal sealed class CompanySlugAllocator(AppDbContext dbContext)
{
    public async Task<string> AllocateAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = CompanySlugGenerator.Generate(companyName);
        var prefix = baseSlug + "-";

        var taken = await dbContext.Companies
            .Where(c => c.Slug != null && (c.Slug == baseSlug || c.Slug.StartsWith(prefix)))
            .Select(c => c.Slug!)
            .ToListAsync(cancellationToken);

        if (taken.Count == 0)
        {
            return baseSlug;
        }

        var takenSet = taken.ToHashSet(StringComparer.Ordinal);
        if (!takenSet.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = CompanySlugGenerator.WithSuffix(baseSlug, n);
            if (!takenSet.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>True when the failed write is the slug index saying "taken" — the one collision
    /// worth retrying rather than surfacing.</summary>
    public static bool IsSlugCollision(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("Slug", StringComparison.Ordinal) == true;
}
