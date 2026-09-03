using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class TrackedJobEndpoints
{
    public static IEndpointRouteBuilder MapTrackedJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tracked-jobs").WithTags("TrackedJobs").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (ClaimsPrincipal user, ITrackedJobService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("List the current user's tracked (not-yet-applied) jobs")
            .Produces<IReadOnlyCollection<TrackedJobResponse>>();

        group.MapPost("/", async (CreateTrackedJobRequest request, ClaimsPrincipal user,
                ITrackedJobService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/tracked-jobs/{created.Id}", created);
            })
            .WithValidation<CreateTrackedJobRequest>()
            .WithSummary("Manually save a job to track")
            .Produces<TrackedJobResponse>(StatusCodes.Status201Created);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ITrackedJobService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(user.GetUserId(), id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
            .WithSummary("Stop tracking a job")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/convert", async (Guid id, ConvertTrackedJobRequest request, ClaimsPrincipal user,
                ITrackedJobService service, CancellationToken cancellationToken) =>
            {
                var application = await service.ConvertToApplicationAsync(user.GetUserId(), id, request, cancellationToken);
                return application is not null
                    ? Results.Created($"/api/applications/{application.Id}", application)
                    : Results.NotFound();
            })
            .WithValidation<ConvertTrackedJobRequest>()
            .WithSummary("Convert a tracked job into a real application")
            .WithDescription("Removes the TrackedJob row — the job now lives in /api/applications instead.")
            .Produces<ApplicationDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Mobile-only: the browser extension scrapes the job page DOM directly, but mobile has no
        // page to scrape — only the URL the user shared/pasted. Never fails: an unsupported host
        // or a fetch/parse miss just comes back with null suggestions for the client to fill in.
        group.MapPost("/resolve-link", async (ResolveTrackedJobLinkRequest request, IJobLinkPreviewService previewService,
                CancellationToken cancellationToken) =>
                Results.Ok(await previewService.ResolveAsync(request.JobUrl, cancellationToken)))
            .WithValidation<ResolveTrackedJobLinkRequest>()
            .RequireRateLimiting(DependencyInjection.LinkPreviewRateLimitPolicy)
            .WithSummary("Preview a job posting URL (mobile app)")
            .WithDescription("Best-effort metadata scrape of a linkedin.com or kariyer.net URL. Always 200 — an " +
                             "unsupported host or a failed fetch just comes back with null fields to fill in manually.")
            .Produces<TrackedJobLinkPreviewResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
