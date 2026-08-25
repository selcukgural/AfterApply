using AfterApply.Api.Extensions;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure;

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
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy);

        group.MapPost("/login", async (LoginRequest request, IAuthService authService,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await authService.LoginAsync(request, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Response)
                    : Results.Problem(detail: string.Join(" ", result.Errors), statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy);

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService authService,
                HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var result = await authService.RefreshAsync(request.RefreshToken, GetIpAddress(httpContext), cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result.Response)
                    : Results.Problem(detail: string.Join(" ", result.Errors), statusCode: StatusCodes.Status401Unauthorized);
            })
            .WithValidation<RefreshRequest>()
            .RequireRateLimiting(DependencyInjection.AuthRateLimitPolicy);

        group.MapPost("/logout", async (LogoutRequest request, IAuthService authService, CancellationToken cancellationToken) =>
            {
                await authService.LogoutAsync(request.RefreshToken, cancellationToken);
                return Results.NoContent();
            })
            .WithValidation<LogoutRequest>()
            .RequireAuthorization();

        return app;
    }

    private static string? GetIpAddress(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();

    private static Dictionary<string, string[]> ToErrorDictionary(IReadOnlyCollection<string> errors) =>
        new() { ["error"] = errors.ToArray() };
}
