using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AfterApply.Api.Extensions;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.EmailIntegrations;
using Hangfire;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class EmailForwardingEndpoints
{
    public static IEndpointRouteBuilder MapEmailForwardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/email-forwarding").WithTags("EmailForwarding")
            .WithDescription("Hidden behind EmailForwarding:Enabled — every endpoint in this group 404s while the flag is off.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<EmailForwardingOptions>>();
            return options.Value.Enabled ? await next(context) : Results.NotFound();
        });

        group.MapGet("/address", async (ClaimsPrincipal user, IEmailForwardingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetOrCreateInboundAddressAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Get (creating on first call) the current user's personal forwarding address, " +
                "plus any pending Gmail forwarding-confirmation code/link")
            .Produces<InboundAddressResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/gmail-confirmation/dismiss", async (ClaimsPrincipal user, IEmailForwardingService service, CancellationToken cancellationToken) =>
            {
                var dismissed = await service.DismissGmailConfirmationAsync(user.GetUserId(), cancellationToken);
                return dismissed ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization()
            .WithSummary("Clear a pending Gmail forwarding-confirmation code once the user has confirmed it in Gmail")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Called by the Cloudflare Email Worker, not by clients — anonymous at the ASP.NET Core
        // auth-middleware level (the Worker holds no user JWT), authenticated instead by the
        // X-Webhook-Secret header checked in the filter below. Rate-limited per inbound token
        // (see RateLimiting.cs) since each accepted request enqueues a job that can trigger a paid
        // LLM classification call.
        //
        // Enqueued via Hangfire rather than awaited inline: the actual work (classification/
        // extraction) can call OpenAI, and the Worker's own fetch() has no retry of its own (see
        // email-worker/src/index.js) — Hangfire's automatic-retry gives that resilience for free,
        // and the Worker no longer has to wait out an LLM round-trip to get its 204.
        group.MapPost("/inbound", (InboundEmailWebhookRequest request, IBackgroundJobClient jobClient) =>
            {
                jobClient.Enqueue<IEmailForwardingService>(s => s.ProcessInboundEmailAsync(new InboundEmailRequest(
                    request.To, request.From, request.FromName ?? request.From, request.Subject ?? "",
                    request.Snippet ?? "", request.ReceivedAt ?? DateTimeOffset.UtcNow), CancellationToken.None));
                return Results.NoContent();
            })
            .AddEndpointFilter(async (context, next) =>
            {
                var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<EmailForwardingOptions>>();
                var expected = options.Value.WebhookSecret;
                var provided = context.HttpContext.Request.Headers["X-Webhook-Secret"].FirstOrDefault();

                if (string.IsNullOrEmpty(expected) || provided is null ||
                    !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
                {
                    return Results.Unauthorized();
                }

                return await next(context);
            })
            .RequireRateLimiting(DependencyInjection.InboundEmailRateLimitPolicy)
            .WithSummary("Receive a forwarded email from the Cloudflare Email Worker — not for direct client use")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/suggestions", async (ClaimsPrincipal user, IEmailForwardingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetPendingSuggestionsAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization()
            .WithSummary("List pending status suggestions detected from the user's forwarded email")
            .Produces<IReadOnlyList<EmailSuggestionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/suggestions/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, IEmailForwardingService service,
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
            .RequireAuthorization()
            .WithSummary("Apply a suggested status change to its application")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/suggestions/{id:guid}/dismiss", async (Guid id, ClaimsPrincipal user, IEmailForwardingService service, CancellationToken cancellationToken) =>
            {
                var dismissed = await service.DismissSuggestionAsync(user.GetUserId(), id, cancellationToken);
                return dismissed ? Results.NoContent() : Results.NotFound();
            })
            .RequireAuthorization()
            .WithSummary("Dismiss a pending suggestion without applying it")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

/// <summary>Shape the Cloudflare Worker POSTs. `To` (the full forwarding address) is how the user is
/// identified — the X-Inbound-Token header carries the same token separately, only so the rate
/// limiter can partition per-sender without parsing the body (rate limiting runs before model
/// binding).</summary>
public sealed record InboundEmailWebhookRequest(
    string To, string From, string? FromName, string? Subject, string? Snippet, DateTimeOffset? ReceivedAt);
