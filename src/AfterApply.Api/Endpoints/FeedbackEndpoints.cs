using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Feedback;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").WithTags("Feedback").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Write-only for now, and scoped to the caller: reading feedback back is the "what happened
        // to what I sent?" screen, which is a decided but deliberately later piece of work
        // (2026-09-07). Until it exists there is no reason to expose a read route at all.
        group.MapPost("/", async (SubmitFeedbackRequest request, ClaimsPrincipal user, HttpContext httpContext,
                IFeedbackService service, CancellationToken cancellationToken) =>
            {
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var created = await service.SubmitAsync(user.GetUserId(), request, userAgent, cancellationToken);
                return Results.Ok(created);
            })
            .WithValidation<SubmitFeedbackRequest>()
            .RequireRateLimiting(DependencyInjection.FeedbackRateLimitPolicy)
            .WithSummary("Send in-app feedback")
            .WithDescription("Stores the message against the calling user. When the GitHub mirror is " +
                             "configured a background job also opens a redacted issue for triage.")
            .Produces<FeedbackResponse>()
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
