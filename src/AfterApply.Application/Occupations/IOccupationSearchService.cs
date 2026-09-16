using AfterApply.Application.Occupations.Contracts;

namespace AfterApply.Application.Occupations;

public interface IOccupationSearchService
{
    Task<IReadOnlyList<OccupationSearchResultResponse>> SearchAsync(string query, CancellationToken cancellationToken);
}
