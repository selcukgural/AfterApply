using System.Security.Claims;
using System.Text.Encodings.Web;
using AfterApply.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Infrastructure.Identity;

/// <summary>Authenticates requests bearing `Authorization: Bearer aa_pat_...` (a Personal Access
/// Token, Sprint 9) against the PersonalAccessTokens table, instead of validating a JWT. Only
/// reached for requests the policy scheme in DependencyInjection.AddIdentityAndJwt routes here —
/// it never runs for a normal JWT bearer token. Builds a ClaimsPrincipal carrying just the `sub`
/// claim (the only one anything in this codebase reads off ClaimsPrincipal — see
/// ClaimsPrincipalExtensions.GetUserId/RateLimiting), so downstream code can't tell a PAT-
/// authenticated request from a JWT-authenticated one.</summary>
internal sealed class PersonalAccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPersonalAccessTokenService personalAccessTokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var value = authorizationHeader.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var rawToken = value["Bearer ".Length..].Trim();
        var userId = await personalAccessTokenService.ValidateAsync(rawToken, Context.RequestAborted);
        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid or revoked personal access token.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString())
        ], PersonalAccessTokenDefaults.AuthenticationScheme);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), PersonalAccessTokenDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}
