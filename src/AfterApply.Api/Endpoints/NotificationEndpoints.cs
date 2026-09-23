using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// The bell (DECISIONS.md 2026-09-23): "your contribution was found helpful" rows and the Gmail
/// scan's status changes as one list. The older <c>/api/email-forwarding/notifications*</c> routes
/// stay for the Gmail rows alone; the web reads this group.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async ([AsParameters] GetNotificationFeedQuery query, ClaimsPrincipal user,
                INotificationFeedService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListAsync(user.GetUserId(), query, cancellationToken)))
            .WithValidation<GetNotificationFeedQuery>()
            .WithSummary("List the bell's rows, newest first")
            .WithDescription("Contribution notifications (a review, salary, candidate experience or blog comment of the " +
                             "caller's was found helpful — how many times that day, never by whom) merged with the " +
                             "Gmail scan's status changes. Gmail rows are left out while the caller has them switched " +
                             "off. Page is capped at 100.")
            .Produces<PagedResult<NotificationFeedItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/count", async (ClaimsPrincipal user, INotificationFeedService service, CancellationToken cancellationToken) =>
                Results.Ok(new NotificationFeedCountResponse(await service.CountUnreadAsync(user.GetUserId(), cancellationToken))))
            .WithSummary("Unread count for the bell's badge — the cheap poll target")
            .Produces<NotificationFeedCountResponse>();

        group.MapPost("/read", async (ClaimsPrincipal user, INotificationFeedService service, CancellationToken cancellationToken) =>
            {
                await service.MarkAllReadAsync(user.GetUserId(), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Mark every row read (fired when the bell's panel or the page opens)")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal user, INotificationFeedService service,
                CancellationToken cancellationToken) =>
            {
                var dismissed = await service.DismissAsync(user.GetUserId(), id, cancellationToken);
                return dismissed ? Results.NoContent() : Results.NotFound();
            })
            .WithSummary("Clear one row off the bell")
            .WithDescription("Takes the id of either kind of row. Hides it only: the mark, the suggestion and any status " +
                             "change stay. Idempotent; 404 when the row is not the caller's.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/dismiss-all", async (ClaimsPrincipal user, INotificationFeedService service, CancellationToken cancellationToken) =>
            {
                await service.DismissAllAsync(user.GetUserId(), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Clear every row the bell shows")
            .Produces(StatusCodes.Status204NoContent);

        var preferences = app.MapGroup("/api/users/me/notification-preferences").WithTags("Notifications").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        preferences.MapGet("/", async (ClaimsPrincipal user, INotificationFeedService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetPreferencesAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("What the bell may tell the caller")
            .Produces<NotificationPreferencesResponse>();

        preferences.MapPut("/", async (UpdateNotificationPreferencesRequest request, ClaimsPrincipal user,
                INotificationFeedService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdatePreferencesAsync(user.GetUserId(), request, cancellationToken)))
            .WithSummary("Change what the bell may tell the caller")
            .WithDescription("Every field is sent every time. A contribution kind that is off is never written — turning it " +
                             "back on does not bring back the marks made meanwhile. Gmail off only hides its rows.")
            .Produces<NotificationPreferencesResponse>();

        return app;
    }
}
