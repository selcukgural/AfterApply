using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;

namespace AfterApply.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications").WithTags("Applications").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async ([AsParameters] GetApplicationsQuery query, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAllAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<GetApplicationsQuery>()
            .WithSummary("List the current user's applications")
            .WithDescription("Paged, filterable by status/company/date range — see GetApplicationsQuery's query parameters.")
            .Produces<PagedResult<ApplicationSummaryResponse>>();

        group.MapGet("/summary", async (ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSummaryCountsAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("Get application counts by status")
            .Produces<ApplicationSummaryCountsResponse>();

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var application = await service.GetByIdAsync(user.GetUserId(), id, cancellationToken);
            return application is not null ? Results.Ok(application) : Results.NotFound();
        })
            .WithSummary("Get a single application")
            .Produces<ApplicationDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateApplicationRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(user.GetUserId(), request, cancellationToken);
                return Results.Created($"/api/applications/{created.Id}", created);
            })
            .WithValidation<CreateApplicationRequest>()
            .WithSummary("Manually log a new application")
            .Produces<ApplicationDetailResponse>(StatusCodes.Status201Created);

        group.MapPost("/from-extension", async (CreateFromExtensionRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateFromExtensionAsync(user.GetUserId(), request, cancellationToken);
                return result.WasDuplicate
                    ? Results.Ok(result)
                    : Results.Created($"/api/applications/{result.Application.Id}", result);
            })
            .WithValidation<CreateFromExtensionRequest>()
            .WithSummary("Log an application from the browser extension's \"I Applied\" action")
            .WithDescription("Deduplicates by JobUrl for this user: clicking it again on the same job page returns the " +
                             "existing application (200, WasDuplicate: true) instead of creating a second one (201).")
            .Produces<ExtensionApplicationResponse>(StatusCodes.Status201Created)
            .Produces<ExtensionApplicationResponse>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateApplicationRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(user.GetUserId(), id, request, cancellationToken);
                return updated is not null ? Results.Ok(updated) : Results.NotFound();
            })
            .WithValidation<UpdateApplicationRequest>()
            .WithSummary("Update an application's editable fields")
            .Produces<ApplicationDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteAsync(user.GetUserId(), id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
            .WithSummary("Delete an application")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/status", async (Guid id, ChangeStatusRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.ChangeStatusAsync(user.GetUserId(), id, request, cancellationToken);
                return updated is not null ? Results.Ok(updated) : Results.NotFound();
            })
            .WithValidation<ChangeStatusRequest>()
            .WithSummary("Change an application's status")
            .WithDescription("Also appends a StatusChanged event to the application's timeline.")
            .Produces<ApplicationDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/timeline", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var timeline = await service.GetTimelineAsync(user.GetUserId(), id, cancellationToken);
            return timeline is not null ? Results.Ok(timeline) : Results.NotFound();
        })
            .WithSummary("Get an application's event timeline")
            .Produces<IReadOnlyCollection<ApplicationEventResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/events", async (Guid id, CreateEventRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
            {
                var created = await service.AddEventAsync(user.GetUserId(), id, request, cancellationToken);
                return created is not null
                    ? Results.Created($"/api/applications/{id}/timeline", created)
                    : Results.NotFound();
            })
            .WithValidation<CreateEventRequest>()
            .WithSummary("Add a manual event to an application's timeline")
            .Produces<ApplicationEventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
