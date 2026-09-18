using AfterApply.Application.CompanyReviews.Contracts;

namespace AfterApply.Application.CompanyReviews;

/// <summary>One row's place in the merged list: which table, which id, when.</summary>
public readonly record struct ContributionStamp(ContributionKind Kind, Guid Id, DateTimeOffset SubmittedAt);

/// <summary>
/// Orders and pages the author's contributions in memory. Three narrow indexed reads (id and
/// timestamp per kind) are cheaper than a UNION across the tables, and the per-user quotas keep
/// the whole set small — tens of rows, a thousand with an admin override — so the ordering rule
/// can live here where a unit test pins it rather than in a query nobody can read.
/// </summary>
public static class ContributionPaging
{
    /// <summary>Newest first; two rows saved in the same instant fall back to id descending,
    /// which for v7 GUIDs is creation order. Pages past the end are empty, never an error.</summary>
    public static (IReadOnlyList<ContributionStamp> Page, int Total) Page(IEnumerable<ContributionStamp> stamps, int page, int pageSize)
    {
        var ordered = stamps
            .OrderByDescending(s => s.SubmittedAt)
            .ThenByDescending(s => s.Id)
            .ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, ordered.Count);
    }
}
