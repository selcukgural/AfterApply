using AfterApply.Application.JobSources.Contracts;
using AfterApply.Application.Pro;
using AfterApply.Domain.Pro;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Pro;

internal sealed class ProEntitlementService(AppDbContext dbContext, TimeProvider? timeProvider = null) : IProEntitlementService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<bool> IsActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.ProEntitlements.AnyAsync(e => e.UserId == userId && e.RevokedAt == null && e.ActiveUntil > now, cancellationToken);

    public async Task<ProEntitlementResponse?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entitlement = await dbContext.ProEntitlements.AsNoTracking()
            .SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
        return entitlement is null ? null : ToResponse(entitlement, _timeProvider.GetUtcNow());
    }

    public async Task<ProEntitlementResponse> GrantAsync(Guid userId, DateTimeOffset activeUntil, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var entitlement = await dbContext.ProEntitlements.SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
        if (entitlement is null)
        {
            entitlement = ProEntitlement.Grant(userId, activeUntil, ProEntitlementSource.Manual, now);
            dbContext.ProEntitlements.Add(entitlement);
        }
        else
        {
            entitlement.Extend(activeUntil, ProEntitlementSource.Manual, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(entitlement, now);
    }

    public async Task<bool> RevokeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entitlement = await dbContext.ProEntitlements.SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
        if (entitlement is null)
        {
            return false;
        }

        entitlement.Revoke(_timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProEntitlementResponse ToResponse(ProEntitlement e, DateTimeOffset now) =>
        new(e.UserId, e.ActiveUntil, e.Source.ToString(), e.GrantedAt, e.RevokedAt, e.IsActive(now));
}
