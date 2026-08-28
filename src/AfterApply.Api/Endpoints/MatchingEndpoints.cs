using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Matching;
using AfterApply.Application.Matching.Contracts;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Matching;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matching").WithTags("Matching").RequireAuthorization();

        // Flag off → 404 for every caller, same pattern as CompanyIntelligenceEndpoints. The
        // feature sends the user's CV text to OpenAI (cross-border transfer) and is hidden until
        // the KVKK disclosure/consent work covering that is done — see DEVELOPMENT_PLAN.md
        // Sprint 8 "Kullanıcıdan gizlendi (2026-08-29)".
        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<MatchingOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        group.MapGet("/profile", async (ClaimsPrincipal user, IJobMatchingService service, CancellationToken cancellationToken) =>
        {
            var profile = await service.GetProfileAsync(user.GetUserId(), cancellationToken);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        });

        group.MapPut("/profile", async (UpdateCandidateProfileRequest request, ClaimsPrincipal user,
                IJobMatchingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateProfileAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<UpdateCandidateProfileRequest>();

        group.MapGet("/applications/{applicationId:guid}", async (Guid applicationId, ClaimsPrincipal user,
            IJobMatchingService service, CancellationToken cancellationToken) =>
        {
            var match = await service.GetMatchAsync(user.GetUserId(), applicationId, cancellationToken);
            return match is not null ? Results.Ok(match) : Results.NotFound();
        });

        group.MapPost("/applications/{applicationId:guid}", async (Guid applicationId, ComputeJobMatchRequest request,
                ClaimsPrincipal user, IJobMatchingService service, CancellationToken cancellationToken) =>
            {
                var match = await service.ComputeMatchAsync(user.GetUserId(), applicationId, request, cancellationToken);
                return match is not null ? Results.Ok(match) : Results.NotFound();
            })
            .WithValidation<ComputeJobMatchRequest>()
            .RequireRateLimiting(DependencyInjection.MatchingRateLimitPolicy);

        return app;
    }
}
