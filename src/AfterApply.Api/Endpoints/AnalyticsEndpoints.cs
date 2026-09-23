using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Analytics;
using AfterApply.Application.Analytics.Contracts;
using AfterApply.Application.Localization;
using Microsoft.Extensions.Localization;

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

        // Read-only, the caller's own applications only, counts only: what the shareable flow
        // card draws. `period` is "30", "90" or "all".
        group.MapGet("/flow", async (string? period, ClaimsPrincipal user, IAnalyticsService service,
                IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                if (!FlowPeriods.TryParse(period, out var flowPeriod))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["period"] = [localizer["ANALYTICS_FLOW_PERIOD_INVALID"]]
                    });
                }

                return Results.Ok(await service.GetFlowAsync(user.GetUserId(), flowPeriod, cancellationToken));
            })
            .WithSummary("Get the current user's application flow counts for the shareable flow card")
            .Produces<ApplicationFlowResponse>()
            .ProducesValidationProblem();

        return app;
    }
}
