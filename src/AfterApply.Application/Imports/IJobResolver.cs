using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports;

/// <summary>
/// The shared Job row for a posting, found by (source, external id) or created. It takes no
/// description (2026-09-24): the row is shared by everyone who captures the posting, so a
/// description scraped by one person's client must not become what the next person reads. The
/// description a user captured lives on their own application; the Job's comes only from a
/// server-side read of the posting (AtsJobEnrichmentService).
/// </summary>
public interface IJobResolver
{
    Task<Guid> ResolveOrCreateAsync(Guid companyId, string title, Source source, string? url,
        string? externalId, string? location, CancellationToken cancellationToken,
        DateTimeOffset? publishedAt = null);
}
