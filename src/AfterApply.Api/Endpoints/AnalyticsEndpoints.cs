using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Analytics;

namespace AfterApply.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").WithTags("Analytics").RequireAuthorization();

        group.MapGet("/overview", async (ClaimsPrincipal user, IAnalyticsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOverviewAsync(user.GetUserId(), cancellationToken)));

        return app;
    }
}
