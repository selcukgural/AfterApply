using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.Admin;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanySalaries;
using AfterApply.Application.CompanySalaries.Contracts;

namespace AfterApply.Api.Endpoints;

/// <summary>The admin's view of the two contribution kinds that have no moderation state —
/// salary entries and candidate experiences (2026-09-18). Same access rule as the review
/// moderation surface: the Users.IsAdmin column, re-read on every request, bare 403 otherwise.
/// A row is listed with its author and can be removed; there is nothing to approve. Every
/// delete leaves a RequestAudit row like any other write.</summary>
public static class AdminContributionEndpoints
{
    public static IEndpointRouteBuilder MapAdminContributionEndpoints(this IEndpointRouteBuilder app)
    {
        var salaries = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization()
            .AddEndpointFilter<CompanySalariesEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        salaries.MapGet("/company-salaries", async ([AsParameters] AdminCompanySalaryListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICompanySalaryAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListAsync(query, cancellationToken));
            })
            .WithValidation<AdminCompanySalaryListQuery>()
            .WithSummary("Every salary entry with its author, newest first")
            .WithDescription("Filter by company name. Admin only; the author's e-mail is on each row.")
            .Produces<PagedResult<AdminCompanySalaryListItemResponse>>();

        salaries.MapDelete("/company-salaries/{entryId:guid}", async (Guid entryId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICompanySalaryAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await service.DeleteAsync(entryId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Remove a salary entry outright")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var experiences = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization()
            .AddEndpointFilter<CandidateExperiencesEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        experiences.MapGet("/candidate-experiences", async ([AsParameters] AdminCandidateExperienceListQuery query, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICandidateExperienceAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListAsync(query, cancellationToken));
            })
            .WithValidation<AdminCandidateExperienceListQuery>()
            .WithSummary("Every candidate experience with its author, newest first")
            .WithDescription("Filter by company name. Admin only; the author's e-mail is on each row.")
            .Produces<PagedResult<AdminCandidateExperienceListItemResponse>>();

        experiences.MapDelete("/candidate-experiences/{experienceId:guid}", async (Guid experienceId, ClaimsPrincipal user,
                IAdminAccessService adminAccess, ICandidateExperienceAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return await service.DeleteAsync(experienceId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Remove a candidate experience outright")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
