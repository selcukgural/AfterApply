using AfterApply.Application.Companies.Contracts;

namespace AfterApply.Application.Companies;

public interface ICompanySearchService
{
    // Ranked autocomplete candidates for a partial, mid-typing company name (web form,
    // extension popup). Below Companies:MinQueryLength, returns an empty list. Only companies the
    // caller may know exist: listed ones, and the ones they applied to or track themselves.
    Task<IReadOnlyList<CompanySearchResultResponse>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken);

    // Single best match for a complete company name (e.g. scraped from LinkedIn), only
    // returned when it clears Companies:FuzzyMatchThreshold. Used exclusively by the browser
    // extension's silent auto-attach — CSV/LinkedIn import and email integration are
    // deliberately out of scope and keep using ICompanyResolver's exact match unchanged.
    Task<Guid?> FindHighConfidenceMatchAsync(string companyName, CancellationToken cancellationToken);

    // The company behind a public-page slug, for a signed-in caller who may know it exists — a
    // listed company, or one they applied to or track. The contribution page opens this way from an
    // accepted application's card, before the company has a public page of its own.
    Task<CompanyReferenceResponse?> FindVisibleBySlugAsync(Guid userId, string slug, CancellationToken cancellationToken);
}
