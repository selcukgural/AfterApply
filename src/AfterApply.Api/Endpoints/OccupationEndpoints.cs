using AfterApply.Api.Extensions;
using AfterApply.Application.Occupations;
using AfterApply.Application.Occupations.Contracts;

namespace AfterApply.Api.Endpoints;

public static class OccupationEndpoints
{
    public static IEndpointRouteBuilder MapOccupationEndpoints(this IEndpointRouteBuilder app)
    {
        // Signed-in: the only consumer today is the salary form. Not behind CompanySalaries:Enabled
        // — the catalogue is generic and a later form may pick from it too. No named rate-limit
        // policy: the global per-user limiter is the backstop, as for /api/companies/search.
        var group = app.MapGroup("/api/occupations").WithTags("Occupations").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search", async ([AsParameters] SearchOccupationsQuery query, IOccupationSearchService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAsync(query.Q, cancellationToken)))
            .WithValidation<SearchOccupationsQuery>()
            .WithSummary("Search the occupation catalogue")
            .WithDescription("Typeahead over the seeded ISCO-08 + curated occupation catalogue. Matches either the " +
                             "Turkish or the English name (case- and Turkish-i-insensitive, trigram-fuzzy) and returns " +
                             "both names; queries under Occupations:MinQueryLength answer an empty list. Retired rows " +
                             "are never returned.")
            .Produces<IReadOnlyList<OccupationSearchResultResponse>>();

        return app;
    }
}
