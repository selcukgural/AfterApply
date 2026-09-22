namespace AfterApply.Application.AtsSources;

/// <summary>Background enrichment of a job captured from an ATS: reads the posting back from that
/// ATS's own public, unauthenticated job-board API and fills in whatever the page scrape left
/// blank. Invoked by Hangfire, so the signature has to stay serializable and stable.</summary>
public interface IAtsJobEnrichmentService
{
    Task EnrichAsync(Guid jobId, CancellationToken cancellationToken);
}
