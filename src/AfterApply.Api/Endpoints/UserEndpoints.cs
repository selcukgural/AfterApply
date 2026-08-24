using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
        {
            var profile = await authService.GetProfileAsync(user.GetUserId(), cancellationToken);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        });

        group.MapPut("/me", async (UpdateProfileRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateProfileAsync(user.GetUserId(), request, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithValidation<UpdateProfileRequest>();

        return app;
    }
}
