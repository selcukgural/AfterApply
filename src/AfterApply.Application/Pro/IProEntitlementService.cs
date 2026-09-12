using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.Pro;

public interface IProEntitlementService
{
    Task<bool> IsActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProEntitlementResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProEntitlementResponse> GrantAsync(Guid userId, DateTimeOffset activeUntil, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid userId, CancellationToken cancellationToken);
}
