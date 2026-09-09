using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class ExtensionPairingEndpoints
{
    public static IEndpointRouteBuilder MapExtensionPairingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/extension-pairing").WithTags("ExtensionPairing");

        // Anonymous by necessity, not by oversight: the caller is a freshly installed extension,
        // and the credential this flow exists to deliver is the one it does not have yet. Nothing
        // is granted here — a started pairing is inert until someone signed in confirms it.
        group.MapPost("/requests", async (
                StartExtensionPairingRequest request, IExtensionPairingService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.StartAsync(request, cancellationToken)))
            .WithValidation<StartExtensionPairingRequest>()
            .RequireRateLimiting(DependencyInjection.ExtensionPairingStartRateLimitPolicy)
            .WithSummary("Start pairing a browser extension with an account")
            .WithDescription("Returns a short code for the user to confirm, the device secret the " +
                             "extension polls with, and the URL to open. No credential is issued here.")
            .Produces<StartedExtensionPairingResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/poll", async (
                PollExtensionPairingRequest request, IExtensionPairingService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.PollAsync(request.DeviceSecret, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithValidation<PollExtensionPairingRequest>()
            .RequireRateLimiting(DependencyInjection.ExtensionPairingPollRateLimitPolicy)
            .WithSummary("Ask whether a pairing has been confirmed yet")
            .WithDescription("Anonymous, authenticated by the device secret from the start call. " +
                             "Carries the personal access token exactly once, on the first poll " +
                             "after approval; a 404 means the secret is unknown and the extension " +
                             "should start over.")
            .Produces<ExtensionPairingPollResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        // The confirmation half needs a real browser session. RequireAuthorization() alone would
        // also accept a Full-scoped personal access token, but not an Extension-scoped one (the
        // default policy's scope requirement), which is what matters here: an extension token must
        // never be able to mint its own successor.
        var review = group.MapGroup("/requests").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        review.MapGet("/{code}", async (
                string code, IExtensionPairingService service, CancellationToken cancellationToken) =>
            {
                var result = await service.GetForReviewAsync(code, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithSummary("Read a pairing request the user is about to confirm")
            .Produces<ExtensionPairingReviewResponse>()
            .Produces(StatusCodes.Status404NotFound);

        review.MapPost("/{code}/approve", async (
                string code, ClaimsPrincipal user, IExtensionPairingService service,
                CancellationToken cancellationToken) =>
            {
                var status = await service.ApproveAsync(user.GetUserId(), code, cancellationToken);
                return status is null ? Results.NotFound() : Results.Ok(new ExtensionPairingReviewStatusResponse(status.Value));
            })
            .WithSummary("Confirm a pairing request")
            .WithDescription("Records who approved. The token is minted when the extension collects " +
                             "it, so an approval nobody collects leaves no credential behind. " +
                             "Answers with the current status when the request is no longer pending.")
            .Produces<ExtensionPairingReviewStatusResponse>()
            .Produces(StatusCodes.Status404NotFound);

        review.MapPost("/{code}/deny", async (
                string code, IExtensionPairingService service, CancellationToken cancellationToken) =>
            {
                var status = await service.DenyAsync(code, cancellationToken);
                return status is null ? Results.NotFound() : Results.Ok(new ExtensionPairingReviewStatusResponse(status.Value));
            })
            .WithSummary("Refuse a pairing request")
            .WithDescription("For the person who opened this page holding a code they did not " +
                             "expect. Terminal — the extension that started it is told no.")
            .Produces<ExtensionPairingReviewStatusResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

/// <summary>What approve/deny answer with. A record rather than the bare enum so the response is a
/// JSON object like every other one in this API.</summary>
public sealed record ExtensionPairingReviewStatusResponse(ExtensionPairingStatus Status);
