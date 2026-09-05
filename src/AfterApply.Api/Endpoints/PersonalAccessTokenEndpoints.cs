using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Api.Endpoints;

public static class PersonalAccessTokenEndpoints
{
    public static IEndpointRouteBuilder MapPersonalAccessTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personal-access-tokens").WithTags("PersonalAccessTokens").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", async (ClaimsPrincipal user, IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("List the current user's personal access tokens")
            .WithDescription("Never returns the raw token value — only metadata (name, scope, created/expires/last-used " +
                             "timestamps). The raw token is shown exactly once, in the Create response below. " +
                             "Expired and revoked tokens are omitted.")
            .Produces<IReadOnlyList<PersonalAccessTokenResponse>>();

        group.MapPost("/", async (CreatePersonalAccessTokenRequest request, ClaimsPrincipal user,
                IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.CreateAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<CreatePersonalAccessTokenRequest>()
            .WithSummary("Create a personal access token (for the browser extension)")
            .WithDescription("Expires after the configured lifetime (PersonalAccessTokens:LifetimeDays, published by GET /api/config). Scope defaults to Extension, which can only reach the " +
                             "endpoints the browser extension actually calls; pass Full for a token with the same " +
                             "access as a browser session.")
            .Produces<CreatedPersonalAccessTokenResponse>();

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
        {
            var revoked = await service.RevokeAsync(user.GetUserId(), id, cancellationToken);
            return revoked ? Results.NoContent() : Results.NotFound();
        })
            .WithSummary("Revoke a personal access token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
