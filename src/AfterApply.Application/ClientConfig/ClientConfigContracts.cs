namespace AfterApply.Application.ClientConfig;

/// <summary>
/// Server-side limits a client needs in order to guide the user *before* a request is rejected —
/// returned by the anonymous <c>GET /api/config</c>. Every value here is also enforced server-side;
/// this only lets the web app say the same thing earlier and in one place. Nothing in it is secret.
/// </summary>
public sealed record ClientConfigResponse(
    PasswordPolicyResponse PasswordPolicy,
    PersonalAccessTokenLimitsResponse PersonalAccessTokens);

/// <summary>Mirrors ASP.NET Identity's <c>PasswordOptions</c>, which is what the server actually
/// validates against — the response is built from that object, not from a copy of the config.</summary>
public sealed record PasswordPolicyResponse(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric);

public sealed record PersonalAccessTokenLimitsResponse(
    int MaxActiveTokens,
    int LifetimeDays);
