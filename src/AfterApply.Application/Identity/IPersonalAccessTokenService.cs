using AfterApply.Application.Identity.Contracts;

namespace AfterApply.Application.Identity;

public interface IPersonalAccessTokenService
{
    Task<CreatedPersonalAccessTokenResponse> CreateAsync(Guid userId, CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalAccessTokenResponse>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken);

    /// <summary>Resolves a raw token presented on the wire to its owning UserId, recording usage —
    /// called by PersonalAccessTokenAuthenticationHandler on every PAT-authenticated request.
    /// Returns null for an unknown, revoked, or malformed token.</summary>
    Task<Guid?> ValidateAsync(string rawToken, CancellationToken cancellationToken);
}
