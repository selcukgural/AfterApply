using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Admin;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Application.Pro;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// The user's side of the weekly job-source sweep — their criteria and what the sweep delivered —
/// plus the admin knobs. Everything 404s while <c>JobSources:Enabled</c> is off, so the feature's
/// existence is not observable before it is turned on. Nothing here triggers a fetch: the sweep
/// runs on its own schedule for paying users, and a request from a user or an admin never
/// reaches LinkedIn.
/// </summary>
public static class JobSourceEndpoints
{
    private const int DefaultTake = 50;

    public static IEndpointRouteBuilder MapJobSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/job-sources").WithTags("JobSources").RequireAuthorization()
            .WithDescription("Hidden behind JobSources:Enabled — every route 404s while the flag is off.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.AddEndpointFilter(FlagFilter);

        group.MapGet("/profile", async (ClaimsPrincipal user, IUserJobSourceProfileService service, CancellationToken cancellationToken) =>
            {
                var profile = await service.GetAsync(user.GetUserId(), cancellationToken);
                return profile is null ? Results.NotFound() : Results.Ok(profile);
            })
            .WithSummary("Read the current user's weekly job-search criteria")
            .Produces<JobSourceProfileResponse>();

        group.MapPut("/profile", async (UpsertJobSourceProfileRequest request, ClaimsPrincipal user,
                IUserJobSourceProfileService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UpsertAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<UpsertJobSourceProfileRequest>()
            .WithSummary("Set the current user's weekly job-search criteria")
            .WithDescription("Up to three job titles and one location. Replaces the whole profile. " +
                             "The titles and location are what the weekly sweep sends to the job source as a search.")
            .Produces<JobSourceProfileResponse>();

        group.MapDelete("/profile", async (ClaimsPrincipal user, IUserJobSourceProfileService service, CancellationToken cancellationToken) =>
                await service.DeleteAsync(user.GetUserId(), cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithSummary("Remove the current user's criteria; the sweep skips them from then on")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/postings", async (int? week, int? take, ClaimsPrincipal user, IUserJobSourceDeliveryService service,
                CancellationToken cancellationToken) =>
            {
                if (week is { } w && !WeekKey.IsValid(w))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["week"] = ["Expected an ISO week as yyyyWW."] });
                }

                return Results.Ok(await service.ListAsync(user.GetUserId(), week, take ?? DefaultTake, cancellationToken));
            })
            .WithSummary("The postings delivered to the current user for a week, with the run summary")
            .WithDescription("'week' is an ISO week as yyyyWW; omitted means the latest week with a run. The 'run' " +
                             "summary says how many candidates there were and how many were left out because the user " +
                             "had already applied to them or had already been shown them.")
            .Produces<JobSourceDeliveriesResponse>()
            .ProducesValidationProblem();

        group.MapGet("/postings/{postingId:guid}", async (Guid postingId, ClaimsPrincipal user, IUserJobSourceDeliveryService service,
                CancellationToken cancellationToken) =>
            {
                var posting = await service.GetAsync(user.GetUserId(), postingId, cancellationToken);
                return posting is null ? Results.NotFound() : Results.Ok(posting);
            })
            .WithSummary("One delivered posting with its description")
            .WithDescription("404 unless the posting was delivered to the calling user — a posting id alone opens nothing.")
            .Produces<JobSourcePostingDetailResponse>();

        MapAdmin(app);
        return app;
    }

    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        // Same shape as AdminEndpoints: RequireAuthorization keeps extension tokens out, the
        // IsAdmin check decides which signed-in user, and a non-admin gets a bare 403.
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.AddEndpointFilter(FlagFilter);
        group.AddEndpointFilter(AdminFilter);

        group.MapGet("/job-sources/settings/{userId:guid}", async (Guid userId, IJobSourceAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetUserSettingsAsync(userId, cancellationToken)))
            .WithSummary("A user's job-source limits (override and effective value)")
            .Produces<UserJobSourceSettingsResponse>();

        group.MapPut("/job-sources/settings/{userId:guid}", async (Guid userId, UpdateUserJobSourceLimitsRequest request,
                IJobSourceAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.UpdateUserLimitsAsync(userId, request, cancellationToken)))
            .WithValidation<UpdateUserJobSourceLimitsRequest>()
            .WithSummary("Override a user's weekly posting cap; null restores the default")
            .Produces<UserJobSourceSettingsResponse>();

        group.MapGet("/job-sources/usage", async (IJobSourceAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetUsageAsync(cancellationToken)))
            .WithSummary("Today's request count against the source, and whether the sweep is stopped")
            .Produces<JobSourceUsageResponse>();

        group.MapGet("/pro/entitlements/{userId:guid}", async (Guid userId, IProEntitlementService service, CancellationToken cancellationToken) =>
            {
                var entitlement = await service.GetAsync(userId, cancellationToken);
                return entitlement is null ? Results.NotFound() : Results.Ok(entitlement);
            })
            .WithSummary("A user's Pro entitlement")
            .Produces<ProEntitlementResponse>();

        group.MapPut("/pro/entitlements/{userId:guid}", async (Guid userId, GrantProEntitlementRequest request,
                IProEntitlementService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GrantAsync(userId, request.ActiveUntil, cancellationToken)))
            .WithValidation<GrantProEntitlementRequest>()
            .WithSummary("Grant or extend a user's Pro entitlement by hand")
            .WithDescription("A subscription record, not a trigger: the weekly sweep picks the user up on its own " +
                             "schedule. The payment integration will write the same row.")
            .Produces<ProEntitlementResponse>();

        group.MapDelete("/pro/entitlements/{userId:guid}", async (Guid userId, IProEntitlementService service, CancellationToken cancellationToken) =>
                await service.RevokeAsync(userId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithSummary("Revoke a user's Pro entitlement")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async ValueTask<object?> FlagFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<JobSourceOptions>>();
        return options.Value.Enabled ? await next(context) : Results.NotFound();
    }

    private static async ValueTask<object?> AdminFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var adminAccess = context.HttpContext.RequestServices.GetRequiredService<IAdminAccessService>();
        var isAdmin = await adminAccess.IsAdminAsync(context.HttpContext.User.GetUserId(), context.HttpContext.RequestAborted);
        return isAdmin ? await next(context) : Results.Forbid();
    }
}
