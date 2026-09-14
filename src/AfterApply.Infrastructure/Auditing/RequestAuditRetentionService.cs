using AfterApply.Application.Auditing;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Auditing;

public sealed class RequestAuditRetentionService(
    AppDbContext dbContext,
    IOptions<RequestAuditOptions> options,
    ILogger<RequestAuditRetentionService> logger) : IRequestAuditRetentionService
{
    public async Task<int> PurgeAnonymousAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.AnonymousRetentionDays);

        var deleted = await dbContext.RequestAudits
            .Where(a => a.UserId == null && a.At < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Purged {Count} anonymous request-audit rows older than {Cutoff}", deleted, cutoff);
        }

        return deleted;
    }
}
