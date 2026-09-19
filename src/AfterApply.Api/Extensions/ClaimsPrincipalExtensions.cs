using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AfterApply.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated user is missing a 'sub' claim.");

        return Guid.Parse(sub);
    }

    /// <summary>
    /// The user id when the request carried a valid token, null otherwise. For routes that are
    /// anonymous by design but personalise one detail for a signed-in reader (the blog's
    /// "liked by me"): the token is used if it happens to be there and ignored if it is stale —
    /// a public route must never answer 401, or the web app's refresh loop fires on a public page.
    /// </summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
    }
}
