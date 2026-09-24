using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Companies;
using AfterApply.Application.Companies.Contracts;
using AfterApply.Infrastructure.Identity;

namespace AfterApply.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies").WithTags("Companies").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search", async ([AsParameters] SearchCompaniesQuery query, ClaimsPrincipal user,
                ICompanySearchService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchAsync(user.GetUserId(), query.Q, cancellationToken)))
            .WithValidation<SearchCompaniesQuery>()
            .AllowExtensionToken()
            .WithSummary("Ranked company-name autocomplete")
            .WithDescription("Below Companies:MinQueryLength characters, returns an empty list rather than a validation error.")
            .Produces<IReadOnlyList<CompanySearchResultResponse>>();

        group.MapGet("/by-slug/{slug}", async (string slug, ClaimsPrincipal user, ICompanySearchService service,
                CancellationToken cancellationToken) =>
            await service.FindVisibleBySlugAsync(user.GetUserId(), slug, cancellationToken) is { } company
                ? Results.Ok(company)
                : Results.NotFound())
            .WithSummary("The company behind a public-page slug, if the caller may know it exists")
            .WithDescription("A listed company (it has a contribution, or enough different applicants), or one the caller " +
                              "applied to or tracks. 404 for any other slug, exactly like one that does not exist.")
            .Produces<CompanyReferenceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
