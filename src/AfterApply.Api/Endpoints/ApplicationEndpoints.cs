using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure.Identity;
using Microsoft.Extensions.Localization;

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

        group.MapGet("/grouped", async ([AsParameters] GetGroupedApplicationsQuery query, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetGroupedByCompanyAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<GetGroupedApplicationsQuery>()
            .WithSummary("List the current user's applications grouped by company")
            .WithDescription("Same row filter as the flat list, but the paged unit is the company: PageSize counts companies. "
                + "Each group carries at most 20 applications and flags HasMore when the company holds more; the rest are "
                + "reachable through GET /api/applications?companyId=. TotalApplicationCount is the matching application count "
                + "across every page, which is what a bulk \"all matching\" selection acts on.")
            .Produces<GroupedApplicationsResponse>();

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
            .AllowExtensionToken()
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

        // Bulk operations. Routed under /bulk rather than as verbs on the collection so they can
        // never be confused with the single-application routes above ("bulk" is not a Guid, so the
        // {id:guid} constraint keeps them apart on its own, but the grouping is for readers).
        group.MapPost("/bulk/status", async (BulkChangeStatusRequest request, ClaimsPrincipal user,
                IApplicationService service, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await service.BulkChangeStatusAsync(user.GetUserId(), request, cancellationToken));
                }
                catch (BulkCountMismatchException mismatch)
                {
                    return CountMismatchProblem(mismatch, localizer);
                }
            })
            .WithValidation<BulkChangeStatusRequest>()
            .WithSummary("Change the status of many applications at once")
            .WithDescription("Applications already in the target status are skipped and reported separately. The " +
                             "response lists what actually moved, and from where, which is what POST /bulk/status/undo " +
                             "takes back. Returns 409 when an all-matching selection no longer matches the count the " +
                             "caller was shown; nothing is changed in that case.")
            .Produces<BulkChangeStatusResponse>()
            .Produces<BulkCountMismatch>(StatusCodes.Status409Conflict);

        group.MapPost("/bulk/status/undo", async (UndoBulkStatusRequest request, ClaimsPrincipal user,
                IApplicationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UndoBulkStatusAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<UndoBulkStatusRequest>()
            .WithSummary("Undo a bulk status change")
            .WithDescription("Each entry names the status the caller last saw; anything that has moved on since is " +
                             "left alone and counted as skipped. The undo appends its own history rows rather than " +
                             "removing the ones it reverses.")
            .Produces<UndoBulkStatusResponse>();

        group.MapPost("/bulk/delete", async (BulkDeleteRequest request, ClaimsPrincipal user,
                IApplicationService service, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await service.BulkDeleteAsync(user.GetUserId(), request, cancellationToken));
                }
                catch (BulkCountMismatchException mismatch)
                {
                    return CountMismatchProblem(mismatch, localizer);
                }
            })
            // POST rather than DELETE: the selection is a body, and a DELETE with a body is poorly
            // supported by proxies and client libraries alike.
            .WithValidation<BulkDeleteRequest>()
            .WithSummary("Permanently delete many applications at once")
            .WithDescription("Irreversible. Everything hanging off each application — timeline, status history, " +
                             "reminders, matched email suggestions — is deleted with it. Returns 409 when an " +
                             "all-matching selection no longer matches the count the caller was shown; nothing is " +
                             "deleted in that case.")
            .Produces<BulkDeleteResponse>()
            .Produces<BulkCountMismatch>(StatusCodes.Status409Conflict);

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

        group.MapGet("/{id:guid}/status-history", async (Guid id, ClaimsPrincipal user, IApplicationService service, CancellationToken cancellationToken) =>
        {
            var history = await service.GetStatusHistoryAsync(user.GetUserId(), id, cancellationToken);
            return history is not null ? Results.Ok(history) : Results.NotFound();
        })
            .WithSummary("Get an application's status change history")
            .WithDescription("Every status transition, newest first, with the note the user wrote and how the change was applied.")
            .Produces<IReadOnlyCollection<ApplicationStatusHistoryResponse>>()
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

    /// <summary>
    /// A stale-screen conflict, carried as ProblemDetails so a caller gets the localized sentence
    /// the rest of the API produces, plus both counts as extensions so a UI can say which way the
    /// list moved and refresh itself. Handled here rather than in DomainExceptionHandler because
    /// this is the one coded exception that is not a 400 and that carries data worth reading.
    /// </summary>
    private static IResult CountMismatchProblem(BulkCountMismatchException mismatch, IStringLocalizer<SharedStrings> localizer) =>
        Results.Problem(
            detail: localizer[mismatch.ErrorCode, mismatch.ActualCount],
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = mismatch.ErrorCode,
                ["expectedCount"] = mismatch.ExpectedCount,
                ["actualCount"] = mismatch.ActualCount
            });
}
