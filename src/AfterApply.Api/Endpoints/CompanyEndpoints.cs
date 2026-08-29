using AfterApply.Api.Extensions;
using AfterApply.Application.Companies;
using AfterApply.Application.Companies.Contracts;

namespace AfterApply.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies").WithTags("Companies").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search", async ([AsParameters] SearchCompaniesQuery query,
                ICompanySearchService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAsync(query.Q, cancellationToken)))
            .WithValidation<SearchCompaniesQuery>()
            .WithSummary("Ranked company-name autocomplete")
            .WithDescription("Below Companies:MinQueryLength characters, returns an empty list rather than a validation error.")
            .Produces<IReadOnlyList<CompanySearchResultResponse>>();

        return app;
    }
}
