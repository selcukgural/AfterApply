using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

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

        // Both Google routes answer 404 while GoogleAuth:ClientId/ClientSecret are unset — the same
        // "feature not deployed here" shape CompanyIntelligence uses — so a stale client can't reach
        // a half-configured flow. GET /api/config tells the web app the same thing up front.
        group.MapPost("/google", async (GoogleSignInRequest request, IAuthService authService,
                IOptions<GoogleAuthOptions> googleAuth, IStringLocalizer<SharedStrings> localizer,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                if (!googleAuth.Value.IsConfigured)
                {
                    return Results.NotFound();
                }

                var result = await authService.GoogleSignInAsync(request, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Response)
                    : Results.Problem(detail: TranslateErrors(result.Errors, localizer), statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithValidation<GoogleSignInRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Sign in with Google (authorization code + PKCE)")
            .WithDescription("Exchanges the authorization code Google redirected back with. Returns either `auth` " +
                              "(the same access/refresh token pair as Login — the Google account was already linked, or " +
                              "its verified email matched an existing account and was linked now) or `pendingSignup` " +
                              "(no account yet: show the complete-your-sign-up form and POST /google/signup). " +
                              "404 when Sign in with Google is not configured on this deployment.")
            .Produces<GoogleSignInResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/google/signup", async (GoogleSignupRequest request, IAuthService authService,
                IOptions<GoogleAuthOptions> googleAuth, IStringLocalizer<SharedStrings> localizer,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                if (!googleAuth.Value.IsConfigured)
                {
                    return Results.NotFound();
                }

                var result = await authService.CompleteGoogleSignupAsync(request, GetIpAddress(httpContext), cancellationToken);
                if (result.Succeeded)
                {
                    return Results.Created("/api/users/me", result.Response);
                }

                // Either the one bare code (expired/tampered signup token) or Identity's already
                // localized descriptions — same split RegisterAsync/LoginAsync document.
                var errors = result.Errors.Select(e => e == "AUTH_GOOGLE_SIGNUP_EXPIRED" ? (string)localizer[e] : e).ToArray();
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["error"] = errors });
            })
            .WithValidation<GoogleSignupRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Create the account for a new Google sign-in")
            .WithDescription("Second step after /google returned `pendingSignup`: creates the account under the Google " +
                              "identity carried by `signupToken` (valid 10 minutes) with the names and privacy consent the " +
                              "user confirmed, and returns the token pair like Register. An expired or tampered signup token " +
                              "is a 400 validation problem. 404 when Sign in with Google is not configured.")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
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

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService authService,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                // Always the same response, whether or not the email is registered — see
                // IAuthService.ForgotPasswordAsync's doc comment. The client shows one static,
                // pre-translated message regardless of this body's content.
                await authService.ForgotPasswordAsync(request, GetIpAddress(httpContext), cancellationToken);
                return Results.NoContent();
            })
            .WithValidation<ForgotPasswordRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Request a password reset email")
            .WithDescription("Always returns 204, regardless of whether the email is registered, to avoid " +
                              "leaking account existence.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.ResetPasswordAsync(request, cancellationToken);
                return result.Succeeded
                    ? Results.NoContent()
                    : Results.ValidationProblem(ToErrorDictionary(result.Errors));
            })
            .WithValidation<ResetPasswordRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy)
            .WithSummary("Reset a password using the token from the forgot-password email")
            .WithDescription("On success, every refresh token for the account is revoked — the caller must log " +
                              "in again with the new password.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
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
