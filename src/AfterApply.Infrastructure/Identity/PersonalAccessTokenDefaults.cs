namespace AfterApply.Infrastructure.Identity;

public static class PersonalAccessTokenDefaults
{
    public const string AuthenticationScheme = "PersonalAccessToken";

    /// <summary>Distinguishes a PAT from a JWT access token on the wire — both arrive as a plain
    /// `Authorization: Bearer &lt;value&gt;` header, so the policy scheme selector (see
    /// DependencyInjection.AddIdentityAndJwt) needs a cheap way to route to the right handler
    /// without trying to JWT-parse every request.</summary>
    public const string TokenPrefix = "aa_pat_";

    /// <summary>Carries the token's PersonalAccessTokenScope on the ClaimsPrincipal so
    /// PersonalAccessTokenScopeHandler can enforce it. Absent on a JWT session, which is exactly
    /// how that handler tells the two credential kinds apart.</summary>
    public const string ScopeClaimType = "pat_scope";
}
