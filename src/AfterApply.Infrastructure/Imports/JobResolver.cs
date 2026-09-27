using AfterApply.Application.Imports;
using AfterApply.Domain.Common;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Imports;

internal sealed class JobResolver(AppDbContext dbContext) : IJobResolver
{
    public async Task<Guid> ResolveOrCreateAsync(Guid companyId, string title, Source source, string? url,
        string? externalId, string? location, CancellationToken cancellationToken,
        DateTimeOffset? publishedAt = null)
    {
        if (externalId is not null)
        {
            var existingId = await dbContext.Jobs
                // A closed posting is final: capturing it again means the site put it back up,
                // which is a new posting (see Job.ClosedAt).
                .Where(j => j.Source == source && j.ExternalId == externalId && j.ClosedAt == null)
                .Select(j => (Guid?)j.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingId is not null)
            {
                return existingId.Value;
            }
        }

        var job = Job.Create(companyId, title, source, DateTimeOffset.UtcNow,
            url: url, externalId: externalId, location: location, publishedAt: publishedAt);
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
