using AfterApply.Application.SiteTraffic;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Domain.SiteTraffic;
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

        await CountPostViewAsync(normalized, cancellationToken);

        return affected > 0;
    }

    /// <summary>
    /// A page view of a published blog or guide post is also one on that post's own tally
    /// (2026-09-29) — the number under the article and in the admin tables. Until then the tally
    /// grew on every server-side fetch of the post, crawlers and link previews included; counting
    /// it here gives it the counter's definition of a reader instead: a browser that ran the page's
    /// script, is not automated, is not an admin, and whose user agent is not a crawler's. Nothing
    /// about the visitor is stored here either — the same in-place increment as before.
    /// </summary>
    private async Task CountPostViewAsync(NormalizedSiteTrafficEvent normalized, CancellationToken cancellationToken)
    {
        if (normalized.Event != SiteTrafficEvent.PageView)
        {
            return;
        }

        // The normalizer has already reduced the path to "/{section}/{slug}" with a slug-shaped
        // slug, or to something that is not a post at all.
        var segments = normalized.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return;
        }

        BlogPostKind? kind = segments[0] switch
        {
            "blog" => BlogPostKind.Blog,
            "guide" => BlogPostKind.Guide,
            _ => null
        };
        if (kind is null)
        {
            return;
        }

        var slug = segments[1];
        await dbContext.BlogPosts
            .Where(p => p.Kind == kind && p.Language == normalized.Locale && p.Slug == slug && p.Status == BlogPostStatus.Published)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
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
