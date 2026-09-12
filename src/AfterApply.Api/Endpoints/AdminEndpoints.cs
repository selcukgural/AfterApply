using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Admin;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Application.Metrics;
using AfterApply.Application.SiteTraffic;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Infrastructure.JobSearch;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class AdminEndpoints
{
    private const int DefaultDays = 30;
    private const int MaxDays = 365;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization() alone already keeps extension-scoped personal access tokens out —
        // the default policy carries PersonalAccessTokenScopeRequirement, and nothing here calls
        // AllowExtensionToken(). The admin check below is about *which* signed-in user, not which
        // kind of credential.
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/metrics", async (int? days, ClaimsPrincipal user, IAdminAccessService adminAccess,
                IProductMetricsService metrics, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    // Bare 403, no body: whether an allowlist exists and who is on it is not
                    // something the response should help a caller work out.
                    return Results.Forbid();
                }

                var window = Math.Clamp(days ?? DefaultDays, 1, MaxDays);
                var snapshots = await metrics.GetRecentAsync(window, cancellationToken);
                return Results.Ok(snapshots);
            })
            .WithSummary("Read stored product metrics, newest day first")
            .WithDescription("Internal. Aggregate counts and rates across the whole product — no " +
                             "per-user data. Access comes from the Users.IsAdmin column — granted by hand " +
                             "with SQL, see DEPLOYMENT.md §3a; everyone else gets 403.")
            .Produces<IReadOnlyList<ProductMetricsDayResponse>>();

        group.MapGet("/site-traffic", async (int? days, ClaimsPrincipal user, IAdminAccessService adminAccess,
                ISiteTrafficService siteTraffic, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var window = Math.Clamp(days ?? DefaultDays, 1, MaxDays);
                return Results.Ok(await siteTraffic.GetRecentAsync(window, cancellationToken));
            })
            .WithSummary("Read public-site visit counts, newest day first")
            .WithDescription("Internal. The other half of the funnel: /api/admin/metrics knows what " +
                             "registered users do, this knows whether anyone arrives at all. Counts " +
                             "only — no visitor is identified, so these rows cannot be joined to a " +
                             "user or to each other.")
            .Produces<IReadOnlyList<SiteTrafficCounterResponse>>();

        group.MapGet("/auto-approval-calibration", async (ClaimsPrincipal user, IAdminAccessService adminAccess,
                IAutoApprovalCalibrationService calibration, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await calibration.ComputeAsync(cancellationToken));
            })
            .WithSummary("Auto-approval accuracy by confidence band")
            .WithDescription("Internal. The evidence for choosing EmailAutoApproval:ConfidenceThreshold, " +
                             "computed from stored suggestions — retroactive for any threshold, and " +
                             "aggregate only. Read RevertRate, not AgreementRate: see the contract's " +
                             "documentation for why the second one flatters the feature.")
            .Produces<AutoApprovalCalibrationResponse>();

        // Per-user job search limits. The daily credit ceiling is what stops one account from
        // spending the product's shared monthly quota, so the account it restrains cannot be the
        // one raising it — hence here, behind the admin check, and not on /api/job-search/settings.
        // 404 while JobSearch:Enabled is off, like the routes it configures.
        var jobSearch = group.MapGroup("/job-search/settings")
            .ProducesProblem(StatusCodes.Status404NotFound);
        jobSearch.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<JobSearchOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        jobSearch.MapGet("/{userId:guid}", async (Guid userId, ClaimsPrincipal user, IAdminAccessService adminAccess,
                IJobSearchSettingsService settings, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var result = await settings.GetForUserAsync(userId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithSummary("Read one user's job search settings (admin)")
            .Produces<JobSearchSettingsResponse>();

        jobSearch.MapPut("/{userId:guid}", async (Guid userId, UpdateJobSearchLimitsRequest request, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IJobSearchSettingsService settings, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                var result = await settings.UpdateLimitsAsync(userId, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithValidation<UpdateJobSearchLimitsRequest>()
            .WithSummary("Override one user's job search limits (admin)")
            .WithDescription("Daily credits, pages per search and ids per details call. Null clears an override " +
                             "back to the global default. Touches only the limit columns — the user's own " +
                             "preferences are theirs.")
            .Produces<JobSearchSettingsResponse>();

        return app;
    }
}
