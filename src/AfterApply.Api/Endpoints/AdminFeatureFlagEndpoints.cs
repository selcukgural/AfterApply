using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Admin;
using AfterApply.Application.FeatureFlags;
using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Application.FeatureFlags.Validators;
using AfterApply.Application.Localization;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// Runtime feature flags (DECISIONS.md 2026-09-27). A switch takes two confirmed steps — prepare,
/// then confirm with the returned token and the flag's name typed out — so a stray click, a
/// replayed request or a forged one from another tab cannot change what the product does.
/// </summary>
public static class AdminFeatureFlagEndpoints
{
    public static IEndpointRouteBuilder MapAdminFeatureFlagEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization() keeps extension-scoped personal access tokens out (see
        // AdminEndpoints); the admin check inside each handler is about which signed-in user.
        var group = app.MapGroup("/api/admin/feature-flags").WithTags("Admin").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (ClaimsPrincipal user, IAdminAccessService adminAccess, IFeatureFlagAdminService service,
                CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await service.ListAsync(cancellationToken));
            })
            .WithSummary("List every feature flag with its default, override and current state")
            .WithDescription("Internal. Enabled is what the product does now: the override when one exists, else the " +
                             "deploy default. MissingPrerequisite names configuration this deployment lacks for the flag " +
                             "to work when on; Couplings names what moves with it (privacy text, money).")
            .Produces<IReadOnlyList<FeatureFlagResponse>>();

        group.MapGet("/history", async (string? flag, int? limit, ClaimsPrincipal user, IAdminAccessService adminAccess,
                IFeatureFlagAdminService service, CancellationToken cancellationToken) =>
            {
                if (!await adminAccess.IsAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Results.Forbid();
                }

                FeatureFlag? only = null;
                if (flag is not null)
                {
                    if (!FeatureFlagNames.TryParse(flag, out var parsed))
                    {
                        return Results.NotFound();
                    }

                    only = parsed;
                }

                var take = Math.Clamp(limit ?? FeatureFlagLimits.DefaultHistory, 1, FeatureFlagLimits.MaxHistory);
                return Results.Ok(await service.GetHistoryAsync(only, take, cancellationToken));
            })
            .WithSummary("Read the flag change history, newest first")
            .WithDescription("Internal. Who switched what, when, from which state to which, and why. The connection's " +
                             "IP is recorded with each change but never returned.")
            .Produces<IReadOnlyList<FeatureFlagChangeResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{flag}/prepare", async (string flag, PrepareFeatureFlagChangeRequest request, ClaimsPrincipal user,
                IAdminAccessService adminAccess, IFeatureFlagAdminService service, IStringLocalizer<SharedStrings> localizer,
                CancellationToken cancellationToken) =>
            {
                var adminId = user.GetUserId();
                if (!await adminAccess.IsAdminAsync(adminId, cancellationToken))
                {
                    return Results.Forbid();
                }

                if (!FeatureFlagNames.TryParse(flag, out var parsed))
                {
                    return Results.NotFound();
                }

                var result = await service.PrepareAsync(parsed, request.Enabled, request.Reason!, adminId, cancellationToken);
                return result.Outcome switch
                {
                    PrepareFeatureFlagOutcome.Ready => Results.Ok(result.Ready),
                    PrepareFeatureFlagOutcome.Unchanged => Conflict(localizer, "FEATURE_FLAG_UNCHANGED"),
                    _ => Conflict(localizer, "FEATURE_FLAG_PREREQUISITE_MISSING", result.Current.MissingPrerequisite)
                };
            })
            .WithValidation<PrepareFeatureFlagChangeRequest>()
            .WithSummary("First confirmation step of a flag switch")
            .WithDescription("Internal. Changes nothing. Enabled true/false sets an override, null removes it (back to " +
                             "the deploy default); Reason is required. Returns the change spelled out and a token that " +
                             "only this admin can redeem, for this change, within five minutes, at /confirm. 409 when " +
                             "there is nothing to change, or when switching on while a prerequisite is missing.")
            .Produces<PrepareFeatureFlagChangeResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{flag}/confirm", async (string flag, ConfirmFeatureFlagChangeRequest request, ClaimsPrincipal user,
                HttpContext httpContext, IAdminAccessService adminAccess, IFeatureFlagAdminService service,
                IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                var adminId = user.GetUserId();
                if (!await adminAccess.IsAdminAsync(adminId, cancellationToken))
                {
                    return Results.Forbid();
                }

                if (!FeatureFlagNames.TryParse(flag, out var parsed))
                {
                    return Results.NotFound();
                }

                var result = await service.ConfirmAsync(parsed, request.ConfirmationToken!, request.ConfirmationPhrase!,
                    adminId, httpContext.GetClientIpAddress(), cancellationToken);
                return result.Outcome switch
                {
                    ConfirmFeatureFlagOutcome.Changed => Results.Ok(result.Flag),
                    ConfirmFeatureFlagOutcome.PhraseMismatch => Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["confirmationPhrase"] = [localizer["FEATURE_FLAG_PHRASE_MISMATCH"]]
                    }),
                    ConfirmFeatureFlagOutcome.InvalidToken => Conflict(localizer, "FEATURE_FLAG_CONFIRMATION_INVALID"),
                    ConfirmFeatureFlagOutcome.Stale => Conflict(localizer, "FEATURE_FLAG_CHANGED_SINCE_PREPARE"),
                    _ => Conflict(localizer, "FEATURE_FLAG_PREREQUISITE_MISSING")
                };
            })
            .WithValidation<ConfirmFeatureFlagChangeRequest>()
            .WithSummary("Second confirmation step: apply the prepared switch")
            .WithDescription("Internal. ConfirmationToken is /prepare's token; ConfirmationPhrase is the flag's name typed " +
                             "exactly (case included). Applies the change on every instance and records it with the admin " +
                             "and the connection's IP. 409 when the token is expired or not this admin's, or when the flag " +
                             "changed after /prepare — the admin starts again from what is there now.")
            .Produces<FeatureFlagResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static IResult Conflict(IStringLocalizer<SharedStrings> localizer, string code, string? prerequisite = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        var detail = localizer[code].Value;
        if (prerequisite is not null)
        {
            extensions["prerequisite"] = prerequisite;
            detail = $"{detail} {localizer[prerequisite].Value}";
        }

        return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: detail, extensions: extensions);
    }
}
