using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Admin;
using AfterApply.Application.Metrics;

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

        return app;
    }
}
