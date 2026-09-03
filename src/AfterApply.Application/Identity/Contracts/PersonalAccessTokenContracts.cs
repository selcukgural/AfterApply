namespace AfterApply.Application.Identity.Contracts;

/// <summary>Scope defaults to Extension (the only consumer today) rather than Full, so the
/// least-privileged token is what you get by not thinking about it.</summary>
public sealed record CreatePersonalAccessTokenRequest(
    string Name,
    PersonalAccessTokenScope Scope = PersonalAccessTokenScope.Extension);

public sealed record PersonalAccessTokenResponse(
    Guid Id,
    string Name,
    PersonalAccessTokenScope Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Only returned once, from the create endpoint — the raw token is never persisted
/// (only its hash) and can't be recovered afterwards, same UX as GitHub/similar PATs.</summary>
public sealed record CreatedPersonalAccessTokenResponse(
    Guid Id,
    string Name,
    string Token,
    PersonalAccessTokenScope Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
