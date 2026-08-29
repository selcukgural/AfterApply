using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Common;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure.EmailIntegrations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class EmailIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapEmailIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/email-integrations").WithTags("EmailIntegrations");

        // Flag off → 404 for every caller, same pattern as MatchingEndpoints/CompanyIntelligenceEndpoints.
        // gmail.readonly grants read access to the user's entire inbox (app-level filtering only,
        // not scope-level) with no CASA security assessment / OAuth restricted-scope verification
        // done yet — hidden until that process completes. See PRIVACY_CHECKLIST.md item 4/7.
        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<EmailIntegrationOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        group.MapGet("/gmail/status", async (ClaimsPrincipal user, IEmailIntegrationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetConnectionStatusAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization();

        group.MapGet("/gmail/connect", async (ClaimsPrincipal user, IEmailIntegrationService service,
                IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                try
                {
                    var authorizationUrl = await service.BuildAuthorizationUrlAsync(user.GetUserId(), cancellationToken);
                    return Results.Ok(new { authorizationUrl });
                }
                catch (CodedException ex)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["gmail"] = [localizer[ex.ErrorCode]] });
                }
            })
            .RequireAuthorization();

        // Anonymous at the ASP.NET Core auth-middleware level — Google's redirect carries no JWT.
        // Authenticated instead via the signed `state` parameter (see HandleCallbackAsync).
        group.MapGet("/gmail/callback", async (string? code, string? state, IEmailIntegrationService service,
            IOptions<EmailIntegrationOptions> emailOptions, CancellationToken cancellationToken) =>
        {
            var baseUrl = emailOptions.Value.FrontendBaseUrl.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Redirect($"{baseUrl}/settings?emailIntegration=error&reason=missing_params");
            }

            var result = await service.HandleCallbackAsync(code, state, cancellationToken);
            return result.Succeeded
                ? Results.Redirect($"{baseUrl}/settings?emailIntegration=success")
                : Results.Redirect($"{baseUrl}/settings?emailIntegration=error&reason={result.ErrorReason}");
        });

        group.MapPost("/gmail/disconnect", async (ClaimsPrincipal user, IEmailIntegrationService service, CancellationToken cancellationToken) =>
            {
                var disconnected = await service.DisconnectAsync(user.GetUserId(), cancellationToken);
                return disconnected ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization();

        group.MapGet("/suggestions", async (ClaimsPrincipal user, IEmailIntegrationService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetPendingSuggestionsAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization();

        group.MapPost("/suggestions/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IEmailIntegrationService service,
                IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                var result = await service.ConfirmSuggestionAsync(user.GetUserId(), id, cancellationToken);
                return result switch
                {
                    ConfirmSuggestionResult.Confirmed => Results.NoContent(),
                    ConfirmSuggestionResult.NoStatusToConfirm => Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["suggestionId"] = [localizer["EMAIL_INTEGRATION_NO_STATUS_TO_CONFIRM"]]
                    }),
                    _ => Results.NotFound()
                };
            })
            .RequireAuthorization();

        group.MapPost("/suggestions/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal user, IEmailIntegrationService service, CancellationToken cancellationToken) =>
            {
                var dismissed = await service.DismissSuggestionAsync(user.GetUserId(), id, cancellationToken);
                return dismissed ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization();

        return app;
    }
}
