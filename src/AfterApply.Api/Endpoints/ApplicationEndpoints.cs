using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;

namespace AfterApply.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications").WithTags("Applications").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(user.GetUserId(), cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var application = await service.GetByIdAsync(user.GetUserId(), id, cancellationToken);
            return application is not null ? Results.Ok(application) : Results.NotFound();
        });

        group.MapPost("/", async (CreateApplicationRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/applications/{created.Id}", created);
            })
            .WithValidation<CreateApplicationRequest>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateApplicationRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), id, request, cancellationToken);
                return updated is not null ? Results.Ok(updated) : Results.NotFound();
            })
            .WithValidation<UpdateApplicationRequest>();

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(user.GetUserId(), id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/status", async (Guid id, ChangeStatusRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.ChangeStatusAsync(user.GetUserId(), id, request, cancellationToken);
                return updated is not null ? Results.Ok(updated) : Results.NotFound();
            })
            .WithValidation<ChangeStatusRequest>();

        group.MapGet("/{id:guid}/timeline", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var timeline = await service.GetTimelineAsync(user.GetUserId(), id, cancellationToken);
            return timeline is not null ? Results.Ok(timeline) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/events", async (Guid id, CreateEventRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var created = await service.AddEventAsync(user.GetUserId(), id, request, cancellationToken);
                return created is not null
                    ? Results.Created($"/api/applications/{id}/timeline", created)
                    : Results.NotFound();
            })
            .WithValidation<CreateEventRequest>();

        return app;
    }
}
