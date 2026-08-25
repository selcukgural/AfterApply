using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Api.Endpoints;

public static class PersonalAccessTokenEndpoints
{
    public static IEndpointRouteBuilder MapPersonalAccessTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personal-access-tokens").WithTags("PersonalAccessTokens").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(user.GetUserId(), cancellationToken)));

        group.MapPost("/", async (CreatePersonalAccessTokenRequest request, ClaimsPrincipal user,
                IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.CreateAsync(user.GetUserId(), request, cancellationToken)))
            .WithValidation<CreatePersonalAccessTokenRequest>();

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IPersonalAccessTokenService service, CancellationToken cancellationToken) =>
        {
            var revoked = await service.RevokeAsync(user.GetUserId(), id, cancellationToken);
            return revoked ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
