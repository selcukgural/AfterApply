namespace AfterApply.Application.Identity.Contracts;

public sealed record CreatePersonalAccessTokenRequest(string Name);

public sealed record PersonalAccessTokenResponse(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

/// <summary>Only returned once, from the create endpoint — the raw token is never persisted
/// (only its hash) and can't be recovered afterwards, same UX as GitHub/similar PATs.</summary>
public sealed record CreatedPersonalAccessTokenResponse(Guid Id, string Name, string Token, DateTimeOffset CreatedAt);
