using System.Security.Claims;
using System.Text.Json;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

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

        group.MapDelete("/me", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var deleted = await authService.DeleteAccountAsync(user.GetUserId(), request.Password, cancellationToken);
                return deleted
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Şifre hatalı."] });
            })
            .WithValidation<DeleteAccountRequest>();

        group.MapGet("/me/export", async (ClaimsPrincipal user, IAuthService authService,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var export = await authService.ExportAccountDataAsync(user.GetUserId(), cancellationToken);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(export, jsonOptions.Value.SerializerOptions);
            return Results.File(bytes, "application/json", $"afterapply-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json");
        });

        return app;
    }
}
