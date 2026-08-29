using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure;
using Microsoft.Extensions.Localization;

namespace AfterApply.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await authService.RegisterAsync(request, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Created("/api/users/me", result.Response)
                    : Results.ValidationProblem(ToErrorDictionary(result.Errors));
            })
            .WithValidation<RegisterRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Register a new account")
            .WithDescription("Creates the account and returns an access/refresh token pair, same shape as Login. " +
                              "A taken email or unmet consent requirement comes back as a 400 validation problem, not a 409.")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", async (LoginRequest request, IAuthService authService,
                IStringLocalizer<SharedStrings> localizer, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await authService.LoginAsync(request, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Response)
                    : Results.Problem(detail: TranslateErrors(result.Errors, localizer), statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Log in with email and password")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService authService,
                IStringLocalizer<SharedStrings> localizer, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await authService.RefreshAsync(request.RefreshToken, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Response)
                    : Results.Problem(detail: TranslateErrors(result.Errors, localizer), statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithValidation<RefreshRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Exchange a refresh token for a new access/refresh token pair")
            .WithDescription("Rotates the refresh token — the one submitted here stops working, use the new one from the response.")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (LogoutRequest request, IAuthService authService, CancellationToken cancellationToken) =>
            {
                await authService.LogoutAsync(request.RefreshToken, cancellationToken);
                return Results.NoContent();
            })
            .WithValidation<LogoutRequest>()
            .RequireAuthorization()
            .WithSummary("Revoke a refresh token")
            .WithDescription("Always returns 204, even if the token was already revoked or unknown — logout is idempotent by design.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static string? GetIpAddress(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();

    private static Dictionary<string, string[]> ToErrorDictionary(IReadOnlyCollection<string> errors) =>
        new() { ["error"] = errors.ToArray() };

    // Login/refresh failures carry a bare error code (AUTH_INVALID_CREDENTIALS, AUTH_INVALID_REFRESH_TOKEN) —
    // unlike register's IdentityError.Description (already localized+formatted by
    // LocalizedIdentityErrorDescriber), these need translating here at the API boundary.
    private static string TranslateErrors(IReadOnlyCollection<string> errorCodes, IStringLocalizer<SharedStrings> localizer) =>
        string.Join(" ", errorCodes.Select(code => (string)localizer[code]));
}
