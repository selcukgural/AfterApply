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
}
