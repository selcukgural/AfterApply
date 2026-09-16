using AfterApply.Infrastructure;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// The two addresses on the API host that are not the API. The web app names
/// <c>api.ekariyerim.com</c> in every page (a preconnect hint), so crawlers fetch its root and
/// robots.txt; Search Console listed the root as a 404. Neither belongs in the OpenAPI document —
/// they are not part of the contract the web or the extension calls.
/// </summary>
public static class SiteRootEndpoints
{
    public static IEndpointRouteBuilder MapSiteRootEndpoints(this IEndpointRouteBuilder app)
    {
        // A person or a crawler at the API's root wants the product: send them to the web app,
        // permanently. GET only, so RequestAuditMiddleware records nothing.
        app.MapGet("/", (IOptions<AppOptions> appOptions) =>
                Results.Redirect(appOptions.Value.WebBaseUrl, permanent: true))
            .ExcludeFromDescription();

        // Nothing on this host is a page to index. The redirect above already keeps the root out;
        // this keeps a crawler from probing further.
        app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /\n", "text/plain"))
            .ExcludeFromDescription();

        return app;
    }
}
