using AfterApply.Application.SiteTraffic;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.SiteTraffic;

internal sealed class SiteTrafficService(AppDbContext dbContext) : ISiteTrafficService
{
    public async Task<bool> RecordAsync(RecordSiteTrafficEventRequest request, CancellationToken cancellationToken)
    {
        var normalized = SiteTrafficNormalizer.Normalize(request.Event, request.Path, request.Referrer);
        if (normalized is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var id = Guid.CreateVersion7();
        var eventName = normalized.Event.ToString();

        // Written as a single ON CONFLICT statement rather than read-modify-write through the
        // change tracker, because concurrency is this table's normal state, not an edge case: every
        // visitor landing on the same page in the same second collides on the same row. A
        // load-then-increment would either lose counts or trip the unique index and need a retry
        // loop; the database does it atomically in one round trip.
        //
        // The conflict target is the same column set as the unique index in
        // SiteTrafficDailyCounterConfiguration — the two must stay together, and an integration test
        // asserts that a second identical report increments rather than duplicating.
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "SiteTrafficDailyCounters"
                 ("Id", "Day", "Event", "Path", "Locale", "ReferrerHost", "Count", "LastSeenAt")
             VALUES
                 ({id}, {day}, {eventName}, {normalized.Path}, {normalized.Locale},
                  {normalized.ReferrerHost}, 1, {now})
             ON CONFLICT ("Day", "Event", "Path", "Locale", "ReferrerHost")
             DO UPDATE SET
                 "Count" = "SiteTrafficDailyCounters"."Count" + 1,
                 "LastSeenAt" = {now}
             """,
            cancellationToken);

        return affected > 0;
    }

    public async Task<IReadOnlyList<SiteTrafficCounterResponse>> GetRecentAsync(
        int days, CancellationToken cancellationToken)
    {
        var from = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(-(days - 1));

        return await dbContext.SiteTrafficDailyCounters
            .AsNoTracking()
            .Where(c => c.Day >= from)
            .OrderByDescending(c => c.Day)
            .ThenByDescending(c => c.Count)
            .ThenBy(c => c.Path)
            .Select(c => new SiteTrafficCounterResponse(
                c.Day, c.Event.ToString(), c.Path, c.Locale, c.ReferrerHost, c.Count))
            .ToListAsync(cancellationToken);
    }
}
