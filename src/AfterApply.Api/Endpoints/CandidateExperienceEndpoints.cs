using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class CandidateExperienceEndpoints
{
    public static IEndpointRouteBuilder MapCandidateExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        // Reading is public, like reviews: there is no free text and nothing worth scraping —
        // ratings, catalogue keys and closed-list facts, dated to a quarter.
        var publicGroup = app.MapGroup("/api/companies/public").WithTags("CandidateExperiences")
            .AddEndpointFilter<CandidateExperiencesEnabledFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        publicGroup.MapGet("/{slug}/experiences", async (string slug, [AsParameters] CandidateExperienceListQuery query,
                ICandidateExperienceService service, CancellationToken cancellationToken) =>
            {
                var page = await service.ListForCompanyAsync(slug, query, cancellationToken);
                return page is null ? Results.NotFound() : Results.Ok(page);
            })
            .WithValidation<CandidateExperienceListQuery>()
            .WithSummary("Candidates' ratings of a company's hiring process")
            .WithDescription("Public and anonymous: no author, no free text, no job title, and the date is quarter " +
                             "precision. Statements are catalogue keys; the wording lives in the web's message " +
                             "catalogue. The summary's score, category averages, most-picked lists and process " +
                             "statistics are null/empty until CandidateExperiences:MinimumEntriesForStats entries exist.")
            .Produces<CandidateExperiencePageResponse>();

        // Everything a signed-in person does. Extension tokens stay out (no AllowExtensionToken):
        // rating a hiring process is a person at a keyboard, never a content script.
        var group = app.MapGroup("/api").WithTags("CandidateExperiences").RequireAuthorization()
            .AddEndpointFilter<CandidateExperiencesEnabledFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/companies/{companyId:guid}/experiences/me", async (Guid companyId, ClaimsPrincipal user,
                ICandidateExperienceService service, CancellationToken cancellationToken) =>
            {
                var state = await service.GetViewerStateAsync(user.GetUserId(), companyId, cancellationToken);
                return state is null ? Results.NotFound() : Results.Ok(state);
            })
            .WithSummary("The caller's own candidate experience for this company, if any, and their quota")
            .Produces<CandidateExperienceViewerStateResponse>();

        group.MapPost("/companies/{companyId:guid}/experiences", async (Guid companyId, CandidateExperienceRequest request,
                ClaimsPrincipal user, ICandidateExperienceService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), companyId, request, cancellationToken);
                return created is null ? Results.NotFound() : Results.Created($"/api/candidate-experiences/{created.Id}", created);
            })
            .WithValidation<CandidateExperienceRequest>()
            .RequireRateLimiting(DependencyInjection.CandidateExperienceWriteRateLimitPolicy)
            .WithSummary("Rate a company's hiring process")
            .WithDescription("Public on save. Only the overall rating is required; category ratings, statement picks " +
                             "(catalogue keys, at most 5 per kind) and the process facts are optional. One entry per " +
                             "company per account, and at most CandidateExperiences:MaxEntriesPerUser in total — both " +
                             "refusals are 400s with a coded detail.")
            .Produces<MyCandidateExperienceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/candidate-experiences/mine", async (ClaimsPrincipal user, ICandidateExperienceService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMineAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The caller's candidate experiences, with quota")
            .Produces<MyCandidateExperiencesResponse>();

        group.MapPut("/candidate-experiences/{experienceId:guid}", async (Guid experienceId, CandidateExperienceRequest request,
                ClaimsPrincipal user, ICandidateExperienceService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), experienceId, request, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithValidation<CandidateExperienceRequest>()
            .RequireRateLimiting(DependencyInjection.CandidateExperienceWriteRateLimitPolicy)
            .WithSummary("Edit the caller's own candidate experience")
            .WithDescription("Replaces the ratings, picks and interview types and resets the entry's date. Another " +
                             "account's entry is 404, not 403.")
            .Produces<MyCandidateExperienceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/candidate-experiences/{experienceId:guid}", async (Guid experienceId, ClaimsPrincipal user,
                ICandidateExperienceService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(user.GetUserId(), experienceId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .RequireRateLimiting(DependencyInjection.CandidateExperienceWriteRateLimitPolicy)
            .WithSummary("Delete the caller's own candidate experience")
            .WithDescription("Frees a quota slot. Another account's entry is 404, not 403.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/candidate-experiences/{experienceId:guid}/helpful", async (Guid experienceId, ClaimsPrincipal user,
                ICandidateExperienceService service, CancellationToken cancellationToken) =>
            {
                var result = await service.ToggleHelpfulAsync(user.GetUserId(), experienceId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireRateLimiting(DependencyInjection.CandidateExperienceHelpfulRateLimitPolicy)
            .WithSummary("Toggle 'helpful' on a candidate experience")
            .WithDescription("Idempotent per account: on, then off. Own experiences are refused with a 400. The first " +
                             "mark from an account tells the author — how many that day, never who.")
            .Produces<HelpfulToggleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
