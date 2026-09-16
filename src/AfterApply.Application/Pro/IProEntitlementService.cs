using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Payments;

namespace AfterApply.Application.Pro;

public interface IProEntitlementService
{
    Task<bool> IsActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProEntitlementResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The admin's by-hand grant: sets an absolute end date, source Manual.</summary>
    Task<ProEntitlementResponse> GrantAsync(Guid userId, DateTimeOffset activeUntil, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A paid order adds one plan period on top of whatever is left (never on top of an
    /// end already in the past), source PayTr. Saves; meant to be called inside the caller's
    /// transaction so the order and the entitlement commit together.</summary>
    Task<ProEntitlementExtension> ExtendForPaymentAsync(Guid userId, ProPlan plan, CancellationToken cancellationToken);

    /// <summary>A full refund takes back the period its order added. Saves. Returns the new end,
    /// or null when the user has no entitlement row (account deleted).</summary>
    Task<DateTimeOffset?> WindBackAsync(Guid userId, TimeSpan by, CancellationToken cancellationToken);
}

/// <summary>What one payment did to the entitlement: the point the period was added onto and the new end.</summary>
public sealed record ProEntitlementExtension(DateTimeOffset PeriodStart, DateTimeOffset ActiveUntil);
