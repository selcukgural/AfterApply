using AfterApply.Application.Admin;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Admin;

internal sealed class AdminAccessService(AppDbContext dbContext) : IAdminAccessService
{
    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        // One indexed lookup by primary key, on every admin request. Cheap enough to skip a cache,
        // and skipping it is what makes a grant or a revoke land on the very next request instead
        // of whenever a cache happened to expire.
        return await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsAdmin)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
