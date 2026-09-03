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
/// it never runs for a normal JWT bearer token. Builds a ClaimsPrincipal carrying `sub` (what
/// ClaimsPrincipalExtensions.GetUserId and RateLimiting read, identically for both credential
/// kinds) plus the token's scope. The scope claim is the one deliberate difference from a JWT
/// principal: its presence is how PersonalAccessTokenScopeHandler recognises a PAT-authenticated
/// request and holds it to the endpoints that token is allowed to reach.</summary>
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
        var validated = await personalAccessTokenService.ValidateAsync(rawToken, Context.RequestAborted);
        if (validated is null)
        {
            return AuthenticateResult.Fail("Invalid, revoked, or expired personal access token.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, validated.UserId.ToString()),
            new Claim(PersonalAccessTokenDefaults.ScopeClaimType, validated.Scope.ToString())
        ], PersonalAccessTokenDefaults.AuthenticationScheme);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), PersonalAccessTokenDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}
