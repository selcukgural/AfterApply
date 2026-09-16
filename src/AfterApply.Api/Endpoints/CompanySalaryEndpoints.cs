using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class CompanySalaryEndpoints
{
    public static IEndpointRouteBuilder MapCompanySalaryEndpoints(this IEndpointRouteBuilder app)
    {
        // One group, all of it behind sign-in — reading included. Unlike reviews there is no
        // anonymous surface: a salary list is worth scraping, and asking for an account is the
        // cheapest ceiling on that. Extension tokens stay out (no AllowExtensionToken): sharing a
        // salary is a person at a keyboard, never a content script.
        var group = app.MapGroup("/api").WithTags("CompanySalaries").RequireAuthorization()
            .AddEndpointFilter<CompanySalariesEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/companies/{companyId:guid}/salaries", async (Guid companyId, [AsParameters] CompanySalaryListQuery query,
                ICompanySalaryService service, CancellationToken cancellationToken) =>
            {
                var page = await service.ListForCompanyAsync(companyId, query, cancellationToken);
                return page is null ? Results.NotFound() : Results.Ok(page);
            })
            .WithValidation<CompanySalaryListQuery>()
            .WithSummary("Salary entries shared for a company")
            .WithDescription("Signed-in readers only. Anonymous rows: the occupation's catalogue names, no author, the years of experience as a band " +
                             "(0–1, 2–4, 5–9, 10+), the date at month precision. Per-currency median, minimum and " +
                             "maximum are null until CompanySalaries:MinimumEntriesForStats entries exist in that currency.")
            .Produces<CompanySalaryPageResponse>();

        group.MapGet("/companies/{companyId:guid}/salaries/me", async (Guid companyId, ClaimsPrincipal user,
                ICompanySalaryService service, CancellationToken cancellationToken) =>
            {
                var state = await service.GetViewerStateAsync(user.GetUserId(), companyId, cancellationToken);
                return state is null ? Results.NotFound() : Results.Ok(state);
            })
            .WithSummary("The caller's own salary entries for this company, and their quota")
            .Produces<CompanySalaryViewerStateResponse>();

        group.MapPost("/companies/{companyId:guid}/salaries", async (Guid companyId, CompanySalaryRequest request,
                ClaimsPrincipal user, ICompanySalaryService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), companyId, request, cancellationToken);
                return created is null ? Results.NotFound() : Results.Created($"/api/company-salaries/{created.Id}", created);
            })
            .WithValidation<CompanySalaryRequest>()
            .RequireRateLimiting(DependencyInjection.CompanySalaryWriteRateLimitPolicy)
            .WithSummary("Share a salary for an occupation at a company")
            .WithDescription("Visible to signed-in readers on save. The occupation is a row of the catalogue " +
                             "(/api/occupations/search) — an unknown or retired id is a 400. One entry per occupation " +
                             "per company per account, and at most " +
                             "CompanySalaries:MaxEntriesPerUser in total — both refusals are 400s with a coded detail.")
            .Produces<MyCompanySalaryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/company-salaries/mine", async (ClaimsPrincipal user, ICompanySalaryService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMineAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The caller's salary entries, with quota")
            .Produces<MySalariesResponse>();

        group.MapPut("/company-salaries/{entryId:guid}", async (Guid entryId, CompanySalaryRequest request,
                ClaimsPrincipal user, ICompanySalaryService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), entryId, request, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithValidation<CompanySalaryRequest>()
            .RequireRateLimiting(DependencyInjection.CompanySalaryWriteRateLimitPolicy)
            .WithSummary("Edit the caller's own salary entry")
            .WithDescription("Resets the entry's month. Another account's entry is 404, not 403.")
            .Produces<MyCompanySalaryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/company-salaries/{entryId:guid}", async (Guid entryId, ClaimsPrincipal user,
                ICompanySalaryService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(user.GetUserId(), entryId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .RequireRateLimiting(DependencyInjection.CompanySalaryWriteRateLimitPolicy)
            .WithSummary("Delete the caller's own salary entry")
            .WithDescription("Frees a quota slot. Another account's entry is 404, not 403.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
