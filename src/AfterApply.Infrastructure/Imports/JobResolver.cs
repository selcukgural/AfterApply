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
        string? description = null, DateTimeOffset? publishedAt = null, string? descriptionHtml = null)
    {
        if (externalId is not null)
        {
            var existingId = await dbContext.Jobs
                .Where(j => j.Source == source && j.ExternalId == externalId)
                .Select(j => (Guid?)j.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingId is not null)
            {
                return existingId.Value;
            }
        }

        var job = Job.Create(companyId, title, source, DateTimeOffset.UtcNow,
            description: description, url: url, externalId: externalId, location: location, publishedAt: publishedAt,
            descriptionHtml: descriptionHtml);
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
