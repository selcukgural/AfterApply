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

    /// <summary>Gives a company its slug if it has none yet and saves. Every write path that can
    /// make a company public — review, salary entry, candidate experience — goes through here,
    /// because the directory lists a company only by its slug: a row without one would take the
    /// contribution and then never show it.</summary>
    public async Task EnsureSlugAsync(Company company, CancellationToken cancellationToken)
    {
        if (company.Slug is not null)
        {
            return;
        }

        // Same retry as CompanyResolver's create path: two contributions making the same company
        // public at once can pick the same suffix, and the second one takes the next.
        for (var attempt = 0; ; attempt++)
        {
            company.AssignSlug(await AllocateAsync(company.Name, cancellationToken), DateTimeOffset.UtcNow);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsSlugCollision(ex))
            {
                // The entry stays tracked; only the slug is re-chosen.
            }
        }
    }

    /// <summary>True when the failed write is the slug index saying "taken" — the one collision
    /// worth retrying rather than surfacing.</summary>
    public static bool IsSlugCollision(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("Slug", StringComparison.Ordinal) == true;
}
