using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Notifications;

namespace AfterApply.Api.Endpoints;

public static class ReminderEndpoints
{
    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reminders").WithTags("Reminders").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IReminderService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetActiveRemindersAsync(user.GetUserId(), cancellationToken)));

        group.MapPost("/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal user, IReminderService service, CancellationToken cancellationToken) =>
        {
            var dismissed = await service.DismissAsync(user.GetUserId(), id, cancellationToken);
            return dismissed ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
