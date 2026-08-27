using AfterApply.Application.Companies.Contracts;

namespace AfterApply.Application.Companies;

public interface ICompanySearchService
{
    // Ranked autocomplete candidates for a partial, mid-typing company name (web form,
    // extension popup). Below Companies:MinQueryLength, returns an empty list.
    Task<IReadOnlyList<CompanySearchResultResponse>> SearchAsync(string query, CancellationToken cancellationToken);

    // Single best match for a complete company name (e.g. scraped from LinkedIn), only
    // returned when it clears Companies:FuzzyMatchThreshold. Used exclusively by the browser
    // extension's silent auto-attach — CSV/LinkedIn import and email integration are
    // deliberately out of scope and keep using ICompanyResolver's exact match unchanged.
    Task<Guid?> FindHighConfidenceMatchAsync(string companyName, CancellationToken cancellationToken);
}
