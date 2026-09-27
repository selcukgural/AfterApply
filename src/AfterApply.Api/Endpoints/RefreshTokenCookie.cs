using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// The web session's refresh token lives in this cookie and nowhere a script can read it
/// (DECISIONS.md 2026-09-27). Host-only on the API's own host, and scoped to <c>/api/auth</c> so it
/// rides along only with the requests that need it — refresh and logout.
/// <para>
/// SameSite=Strict is enough to keep a cross-site page from spending it: the web app and the API
/// are the same site (ekariyerim.com and api.ekariyerim.com; localhost in development). The Origin
/// check on the routes that read it is the second layer, for a same-site origin we don't own.
/// </para>
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "__Secure-ek_rt";

    private const string Path = "/api/auth";

    public static void Append(HttpResponse response, string refreshToken, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt,
            IsEssential = true,
        });

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        });

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>
    /// True when a browser says the request comes from a page that isn't ours. A request with no
    /// Origin at all is not a browser's cross-origin POST (browsers always send one there), so it
    /// passes — it can't carry a victim's cookie without a victim's browser.
    /// </summary>
    public static bool IsForeignOrigin(HttpContext httpContext, IConfiguration configuration)
    {
        var origin = httpContext.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
        {
            return false;
        }

        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        return !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Moves the refresh token of every sign-in response under <c>/api/auth</c> into the cookie. The
    /// token itself is <c>[JsonIgnore]</c> on <see cref="AuthResponse"/>, so the body never carries
    /// it; doing this in one filter means a new sign-in route can't forget the cookie.
    /// </summary>
    public sealed class Filter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var result = await next(context);

            var auth = (result as IValueHttpResult)?.Value switch
            {
                AuthResponse response => response,
                GoogleSignInResponse { Auth: { } response } => response,
                LinkedInSignInResponse { Auth: { } response } => response,
                GitHubSignInResponse { Auth: { } response } => response,
                _ => null,
            };

            if (auth is not null)
            {
                Append(context.HttpContext.Response, auth.RefreshToken, auth.RefreshTokenExpiresAt);
            }

            return result;
        }
    }
}
