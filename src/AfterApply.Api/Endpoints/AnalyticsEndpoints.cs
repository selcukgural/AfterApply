using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Analytics;
using AfterApply.Application.Analytics.Contracts;

namespace AfterApply.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").WithTags("Analytics").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/overview", async (ClaimsPrincipal user, IAnalyticsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOverviewAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("Get the current user's application funnel and response-rate metrics")
            .Produces<AnalyticsOverviewResponse>();

        return app;
    }
}
