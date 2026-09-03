using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

public interface IPersonalAccessTokenService
{
    Task<CreatedPersonalAccessTokenResponse> CreateAsync(Guid userId, CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken);

    /// <summary>Resolves a raw token presented on the wire to its owner and scope, recording usage —
    /// called by PersonalAccessTokenAuthenticationHandler on every PAT-authenticated request.
    /// Returns null for an unknown, revoked, expired, or malformed token.</summary>
    Task<ValidatedPersonalAccessToken?> ValidateAsync(string rawToken, CancellationToken cancellationToken);
}

/// <summary>What a valid token resolves to. Kept as a record rather than a bare Guid so the scope
/// travels with the identity and can be turned into a claim — see
/// PersonalAccessTokenDefaults.ScopeClaimType.</summary>
public sealed record ValidatedPersonalAccessToken(Guid UserId, PersonalAccessTokenScope Scope);
