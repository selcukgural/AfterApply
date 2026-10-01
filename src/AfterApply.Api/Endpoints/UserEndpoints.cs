using System.Security.Claims;
using System.Text.Json;
using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Application.Pro;
using AfterApply.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

public static class UserEndpoints
{
    private const long AvatarRequestSizeLimitBytes = 6 * 1024 * 1024;

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

        group.MapGet("/me/plan", async (ClaimsPrincipal user, IProEntitlementService entitlements, CancellationToken cancellationToken) =>
        {
            var entitlement = await entitlements.GetAsync(user.GetUserId(), cancellationToken);
            return Results.Ok(entitlement is null
                ? new UserPlanResponse(IsActive: false, ActiveUntil: null)
                : new UserPlanResponse(entitlement.IsActive, entitlement.ActiveUntil));
        })
            .WithSummary("Get the current user's Pro status")
            .WithDescription("Not gated by the payments or job-sources flags, unlike /api/payments/plans and " +
                             "/api/job-sources/status: the profile page shows the plan whether or not there is " +
                             "anything to buy. A revoked or expired period comes back inactive with its end date kept.")
            .Produces<UserPlanResponse>();

        // ---- profile photo (DECISIONS.md 2026-09-28) ---------------------------------------------
        // 6 MB for the whole multipart body: the 5 MB file cap plus the form's framing. Anything
        // larger is cut off by the server before the handler reads a byte.
        group.MapPut("/me/avatar", async ([FromForm] IFormFile file, ClaimsPrincipal user,
                IAvatarService avatarService, CancellationToken cancellationToken) =>
            {
                try
                {
                    await using var stream = file.OpenReadStream();
                    var profile = await avatarService.UploadAsync(user.GetUserId(), stream, file.Length, cancellationToken);
                    return profile is not null ? Results.Ok(profile) : Results.NotFound();
                }
                catch (AvatarUploadValidationException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.Field] = [exception.Error] });
                }
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(AvatarRequestSizeLimitBytes))
            .RequireRateLimiting(DependencyInjection.AvatarWriteRateLimitPolicy)
            .WithSummary("Upload or replace the current user's profile photo")
            .WithDescription("multipart/form-data with a 'file' part: JPEG, PNG or WebP, judged by its bytes, up to 5 MB and " +
                             "25 megapixels. The server re-encodes it to a 256×256 WebP with no metadata (EXIF, GPS " +
                             "included) and keeps only that; the upload itself is not stored. Answers the updated " +
                             "profile, whose avatarUrl is new on every upload.")
            .Produces<UserProfileResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/me/avatar", async (ClaimsPrincipal user, IAvatarService avatarService, CancellationToken cancellationToken) =>
            {
                var profile = await avatarService.DeleteAsync(user.GetUserId(), cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .RequireRateLimiting(DependencyInjection.AvatarWriteRateLimitPolicy)
            .WithSummary("Remove the current user's profile photo")
            .WithDescription("Deletes the stored photo and turns showAvatarInComments off. Succeeds when there was no photo.")
            .Produces<UserProfileResponse>()
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me/avatar/visibility", async (UpdateAvatarVisibilityRequest request, ClaimsPrincipal user,
                IAvatarService avatarService, CancellationToken cancellationToken) =>
            {
                var profile = await avatarService.SetShowInCommentsAsync(user.GetUserId(), request.ShowInComments, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .RequireRateLimiting(DependencyInjection.AvatarWriteRateLimitPolicy)
            .WithSummary("Show or hide the profile photo on the current user's blog comments")
            .WithDescription("Off by default. Turning it off also changes avatarUrl, so the address readers already " +
                             "loaded stops resolving. Salary, review, experience and silence-report pages never show " +
                             "the photo, whatever this says.")
            .Produces<UserProfileResponse>()
            .Produces(StatusCodes.Status429TooManyRequests)
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

        group.MapPut("/me/aggregate-contribution", async (UpdateAggregateContributionRequest request, ClaimsPrincipal user,
                IAuthService authService, CancellationToken cancellationToken) =>
            {
                var profile = await authService.UpdateAggregateContributionAsync(user.GetUserId(), request.Contribute, cancellationToken);
                return profile is not null ? Results.Ok(profile) : Results.NotFound();
            })
            .WithSummary("Count, or stop counting, the current user's applications in the anonymous response figures")
            .WithDescription("On by default. Off takes every one of the caller's applications out of the sector " +
                             "response-rate table, the company response tab and the applicant count that lists a " +
                             "company; the caller's own statistics are unaffected. Cached figures catch up within an hour.")
            .Produces<UserProfileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/me", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal user,
                IAuthService authService, IStringLocalizer<SharedStrings> localizer, CancellationToken cancellationToken) =>
            {
                var deleted = await authService.DeleteAccountAsync(user.GetUserId(), request.Password, cancellationToken);
                return deleted
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [localizer["AUTH_WRONG_PASSWORD"]] });
            })
            .ProducesValidationProblem()
            .WithSummary("Permanently delete the current user's account")
            .WithDescription("Requires re-entering the current password in the body. A wrong password comes back as a 400 " +
                             "validation problem on the \"password\" field, not a 401. An account that has no password " +
                             "(created with Sign in with Google, see `hasPassword` on the profile) is deleted without one.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me/export", async (ClaimsPrincipal user, IAuthService authService,
            IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var export = await authService.ExportAccountDataAsync(user.GetUserId(), cancellationToken);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(export, jsonOptions.Value.SerializerOptions);
            return Results.File(bytes, "application/json", $"e-kariyerim-data-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json");
        })
            .WithSummary("Download all of the current user's data as a JSON file")
            .WithDescription("KVKK/GDPR data-portability export — applications, tracked jobs, import batches, reminders, CV documents, feedback, company reviews, salary entries, candidate experiences and blog comments the user shared, the helpful marks they made, the contribution notifications they received, their notification settings and their applications board.")
            .Produces<AccountExportResponse>();

        return app;
    }
}
