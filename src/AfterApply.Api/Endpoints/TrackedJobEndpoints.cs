using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.TrackedJobs;
using AfterApply.Application.TrackedJobs.Contracts;

namespace AfterApply.Api.Endpoints;

public static class TrackedJobEndpoints
{
    public static IEndpointRouteBuilder MapTrackedJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tracked-jobs").WithTags("TrackedJobs").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, ITrackedJobService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(user.GetUserId(), cancellationToken)));

        group.MapPost("/", async (CreateTrackedJobRequest request, ClaimsPrincipal user,
                ITrackedJobService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/tracked-jobs/{created.Id}", created);
            })
            .WithValidation<CreateTrackedJobRequest>();

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ITrackedJobService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(user.GetUserId(), id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/convert", async (Guid id, ConvertTrackedJobRequest request, ClaimsPrincipal user,
                ITrackedJobService service, CancellationToken cancellationToken) =>
            {
                var application = await service.ConvertToApplicationAsync(user.GetUserId(), id, request, cancellationToken);
                return application is not null
                    ? Results.Created($"/api/applications/{application.Id}", application)
                    : Results.NotFound();
            })
            .WithValidation<ConvertTrackedJobRequest>();

        // Mobile-only: the browser extension scrapes the job page DOM directly, but mobile has no
        // page to scrape — only the URL the user shared/pasted. Never fails: an unsupported host
        // or a fetch/parse miss just comes back with null suggestions for the client to fill in.
        group.MapPost("/resolve-link", async (ResolveTrackedJobLinkRequest request, IJobLinkPreviewService previewService,
                CancellationToken cancellationToken) =>
                Results.Ok(await previewService.ResolveAsync(request.JobUrl, cancellationToken)))
            .WithValidation<ResolveTrackedJobLinkRequest>();

        return app;
    }
}
