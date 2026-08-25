using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports;

public interface IJobResolver
{
    Task<Guid> ResolveOrCreateAsync(Guid companyId, string title, Source source, string? url,
        string? externalId, string? location, CancellationToken cancellationToken,
        string? description = null, DateTimeOffset? publishedAt = null);
}
