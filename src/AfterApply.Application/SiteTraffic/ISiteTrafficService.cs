using AfterApply.Application.SiteTraffic.Contracts;

namespace AfterApply.Application.SiteTraffic;

public interface ISiteTrafficService
{
    /// <summary>
    /// Adds one to the counter for this event, or does nothing if the report does not survive
    /// <see cref="SiteTrafficNormalizer"/>. Returns whether a count was actually recorded — for the
    /// tests and the logs; the endpoint answers 204 either way.
    /// </summary>
    Task<bool> RecordAsync(RecordSiteTrafficEventRequest request, CancellationToken cancellationToken);

    /// <summary>Stored counters for the last <paramref name="days"/> days, newest day first.</summary>
    Task<IReadOnlyList<SiteTrafficCounterResponse>> GetRecentAsync(int days, CancellationToken cancellationToken);
}
