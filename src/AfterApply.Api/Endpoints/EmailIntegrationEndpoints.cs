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
        var group = app.MapGroup("/api/email-integrations").WithTags("EmailIntegrations")
            .WithDescription("Hidden behind EmailIntegrations:Enabled — every endpoint in this group 404s for every " +
                             "caller while the flag is off. See PRIVACY_CHECKLIST.md item 4/7.")
            .ProducesProblem(StatusCodes.Status404NotFound);

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
            .RequireAuthorization()
            .WithSummary("Get the current user's Gmail connection status")
            .Produces<EmailConnectionStatusResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
            .RequireAuthorization()
            .WithSummary("Start the Gmail OAuth connection flow")
            .WithDescription("Returns a Google consent-screen URL for the client to redirect the user to; the flow " +
                             "completes at the callback endpoint below.")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        })
            .WithSummary("Google OAuth redirect target — not called directly by clients")
            .WithDescription("Anonymous: authenticated via the signed `state` parameter Google echoes back, not a JWT. " +
                             "Always redirects to the frontend's /settings page, success or failure.")
            .Produces(StatusCodes.Status302Found);

        group.MapPost("/gmail/disconnect", async (ClaimsPrincipal user, IEmailIntegrationService service, CancellationToken cancellationToken) =>
            {
                var disconnected = await service.DisconnectAsync(user.GetUserId(), cancellationToken);
                return disconnected ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization()
            .WithSummary("Disconnect the current user's Gmail account")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // GET /suggestions, POST /suggestions/{id}/confirm, POST /suggestions/{id}/dismiss moved to
        // EmailForwardingEndpoints — they're provider-agnostic (query EmailSuggestions by UserId, not
        // by provider) and now gated by EmailForwarding:Enabled instead, since that's the path that's
        // actually shipping; this group stays Gmail-OAuth-specific and stays off.

        return app;
    }
}
