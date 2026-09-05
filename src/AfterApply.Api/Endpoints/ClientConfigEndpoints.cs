using AfterApply.Application.ClientConfig;
using AfterApply.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

public static class ClientConfigEndpoints
{
    public static IEndpointRouteBuilder MapClientConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous on purpose: the register and reset-password forms need the password rules
        // before there is an account. Not under the Auth rate-limit policy either — that bucket is
        // 5/min per IP and the form fetching its own rules would spend one of the five attempts a
        // user gets at actually registering. The global per-IP limiter still applies.
        app.MapGet("/api/config", (
                IOptions<IdentityOptions> identityOptions,
                IOptions<PersonalAccessTokenOptions> personalAccessTokenOptions,
                HttpContext httpContext) =>
            {
                // Read from IdentityOptions rather than IdentityPolicyOptions: the former is the object
                // PasswordValidator enforces, so whatever ends up here is by construction what a
                // submitted password will be checked against.
                var password = identityOptions.Value.Password;
                var tokens = personalAccessTokenOptions.Value;

                // The values change only with a deploy or a config rollout, so let browsers and the
                // CDN hold them for a few minutes instead of re-fetching on every form mount.
                httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromMinutes(5)
                };

                return Results.Ok(new ClientConfigResponse(
                    new PasswordPolicyResponse(
                        password.RequiredLength,
                        password.RequiredUniqueChars,
                        password.RequireDigit,
                        password.RequireLowercase,
                        password.RequireUppercase,
                        password.RequireNonAlphanumeric),
                    new PersonalAccessTokenLimitsResponse(tokens.MaxActiveTokens, tokens.LifetimeDays)));
            })
            .WithTags("Config")
            .WithSummary("Public client configuration")
            .WithDescription("The server-side limits a client should show the user up front: the password policy " +
                              "(what register and reset-password enforce) and the personal-access-token limits. " +
                              "Anonymous; nothing here is secret. All of it is still enforced server-side.")
            .Produces<ClientConfigResponse>();

        return app;
    }
}
