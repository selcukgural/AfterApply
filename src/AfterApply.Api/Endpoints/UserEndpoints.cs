using System.Security.Claims;
using System.Text.Json;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Localization;
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

        group.MapPut("/me/language", async (UpdateLanguageRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateLanguageAsync(user.GetUserId(), request.Language, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithValidation<UpdateLanguageRequest>();

        group.MapDelete("/me", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request, ClaimsPrincipal user,
                IAuthService authService, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                var deleted = await authService.DeleteAccountAsync(user.GetUserId(), request.Password, cancellationToken);
                return deleted
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [localizer["AUTH_WRONG_PASSWORD"]] });
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
