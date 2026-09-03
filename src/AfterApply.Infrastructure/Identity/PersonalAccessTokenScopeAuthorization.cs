using System.Security.Claims;
using AfterApply.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AfterApply.Infrastructure.Identity;

/// <summary>Marker metadata put on the handful of endpoints an Extension-scoped personal access
/// token is allowed to reach. Presence on the endpoint is the whole check — see
/// <see cref="PersonalAccessTokenScopeHandler"/>.</summary>
public sealed class ExtensionTokenAllowedMetadata;

public static class ExtensionTokenEndpointExtensions
{
    /// <summary>Marks an endpoint as reachable by an Extension-scoped personal access token.
    /// Everything else in the API is, by default, JWT-session-or-Full-scope-token only — so the
    /// safe outcome is what you get by forgetting to call this, and the deliberate one is what you
    /// have to type. Applied to exactly the three authenticated endpoints extension/ calls:
    /// POST /api/applications/from-extension, POST /api/email-forwarding/extension-signal, and
    /// GET /api/companies/search. (Its fourth call, /local-filter-config, is anonymous.)</summary>
    public static TBuilder AllowExtensionToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ExtensionTokenAllowedMetadata());
        return builder;
    }
}

public sealed class PersonalAccessTokenScopeRequirement : IAuthorizationRequirement;

/// <summary>
/// Holds an Extension-scoped personal access token to the endpoints marked with
/// <see cref="ExtensionTokenEndpointExtensions.AllowExtensionToken{TBuilder}"/>, and lets every
/// other caller through untouched.
///
/// Wired into the *default* authorization policy (DependencyInjection.AddIdentityAndJwt) rather
/// than added per-endpoint, because the property worth having is the negative one: an endpoint
/// added later is out of reach for extension tokens unless someone opts it in. A JWT session
/// carries no scope claim at all and is unaffected.
/// </summary>
internal sealed class PersonalAccessTokenScopeHandler : AuthorizationHandler<PersonalAccessTokenScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PersonalAccessTokenScopeRequirement requirement)
    {
        var scope = context.User.FindFirstValue(PersonalAccessTokenDefaults.ScopeClaimType);

        // No scope claim means a JWT session; Full means a token deliberately issued with session-
        // equivalent access. Neither is restricted here.
        if (scope != nameof(PersonalAccessTokenScope.Extension))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Under endpoint routing the authorization middleware passes the HttpContext as the
        // resource, which is how the requirement gets at the endpoint's metadata. Failing closed
        // when it isn't an HttpContext is intentional: an Extension token should never be granted
        // access by a code path this handler can't actually inspect.
        if (context.Resource is HttpContext httpContext &&
            httpContext.GetEndpoint()?.Metadata.GetMetadata<ExtensionTokenAllowedMetadata>() is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
