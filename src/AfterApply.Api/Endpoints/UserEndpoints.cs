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
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
        {
            var profile = await authService.GetProfileAsync(user.GetUserId(), cancellationToken);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        })
            .WithSummary("Get the current user's profile")
            .Produces<UserProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me", async (UpdateProfileRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateProfileAsync(user.GetUserId(), request, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithValidation<UpdateProfileRequest>()
            .WithSummary("Update the current user's name")
            .Produces<UserProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me/language", async (UpdateLanguageRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateLanguageAsync(user.GetUserId(), request.Language, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithValidation<UpdateLanguageRequest>()
            .WithSummary("Update the current user's preferred language")
            .Produces<UserProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me/theme", async (UpdateThemeRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateThemeAsync(user.GetUserId(), request.Theme, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithValidation<UpdateThemeRequest>()
            .WithSummary("Update the current user's preferred theme")
            .Produces<UserProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/me", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request, ClaimsPrincipal user,
                IAuthService authService, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                var deleted = await authService.DeleteAccountAsync(user.GetUserId(), request.Password, cancellationToken);
                return deleted
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [localizer["AUTH_WRONG_PASSWORD"]] });
            })
            .WithValidation<DeleteAccountRequest>()
            .WithSummary("Permanently delete the current user's account")
            .WithDescription("Requires re-entering the current password in the body. A wrong password comes back as a 400 " +
                             "validation problem on the \"password\" field, not a 401.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me/export", async (ClaimsPrincipal user, IAuthService authService,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var export = await authService.ExportAccountDataAsync(user.GetUserId(), cancellationToken);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(export, jsonOptions.Value.SerializerOptions);
            return Results.File(bytes, "application/json", $"e-kariyerim-data-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json");
        })
            .WithSummary("Download all of the current user's data as a JSON file")
            .WithDescription("KVKK/GDPR data-portability export — applications, tracked jobs, import batches, and reminders.")
            .Produces<AccountExportResponse>();

        return app;
    }
}
