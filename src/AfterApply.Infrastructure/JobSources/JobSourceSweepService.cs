using AfterApply.Application.Imports;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The weekly run, in the order the product plan fixes it:
/// <list type="number">
/// <item>Pick the users worth spending on — paying, with criteria, with a CV, seen recently.</item>
/// <item>Fetch each distinct query they share, at most once per share window, page by page,
/// inside the day's request budget, and stop the whole run the moment the source says no.</item>
/// <item>Hand each user their week's postings: the newest ranks first, round-robin across their
/// titles, minus what they already applied to and what they were already shown, up to their
/// weekly cap; write the counts down so the product can say what was left out.</item>
/// <item>Fetch the description for the postings that were actually delivered and lack one.</item>
/// <item>Forget postings nobody has seen in a while and were never delivered.</item>
/// </list>
/// Idempotent within a week: a second run delivers nothing new, so a job that fires late (Cloud
/// Run at zero instances) or twice does no harm. Hangfire's storage schedules a recurring job
/// once however many instances run a server, so there is no overlap to guard against.
///
/// Logs carry counts only — never a keyword, a location or a URL.
/// </summary>
internal sealed class JobSourceSweepService(
    AppDbContext dbContext,
    ILinkedInJobSourceClient client,
    IOptions<JobSourceOptions> options,
    ILogger<JobSourceSweepService> logger,
    TimeProvider? timeProvider = null) : IJobSourceSweepService
{
    private const int LedgerRetentionDays = 90;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (!o.Enabled)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var budget = await LoadBudgetAsync(now, cancellationToken);
        if (budget.InCooldown(now))
        {
            logger.LogWarning("Job source sweep skipped: the source blocked us at {BlockedAt}; cooldown until {CooldownUntil}",
                budget.LastBlockedAt, budget.CooldownUntil);
            return;
        }

        var users = await SelectEligibleUsersAsync(now, cancellationToken);
        if (users.Count == 0)
        {
            logger.LogInformation("Job source sweep: no eligible users");
            return;
        }

        var queryIds = users.SelectMany(u => u.Profile.Queries.Select(q => q.QueryId)).Distinct().ToList();
        var queries = await dbContext.JobSourceQueries.Where(q => queryIds.Contains(q.Id)).ToListAsync(cancellationToken);
        var shareWindow = TimeSpan.FromDays(o.QueryShareDays);

        var fetched = 0;
        var stopped = false;
        foreach (var query in queries.Where(q => !q.RanWithin(shareWindow, now)))
        {
            if (stopped || !budget.CanSpend(_timeProvider.GetUtcNow()))
            {
                break;
            }

            stopped = await RunQueryAsync(query, budget, cancellationToken);
            fetched++;
        }

        var weekKey = WeekKey.From(now);
        var delivered = 0;
        foreach (var user in users)
        {
            delivered += await DeliverAsync(user, weekKey, now, cancellationToken);
        }

        var detailed = stopped ? 0 : await FetchDetailsAsync(weekKey, budget, cancellationToken);
        await PruneAsync(now, cancellationToken);

        logger.LogInformation(
            "Job source sweep: {Users} users, {Queries} queries fetched, {Delivered} postings delivered, {Detailed} details fetched, " +
            "{Requests} requests today, stopped by source: {Stopped}",
            users.Count, fetched, delivered, detailed, budget.RequestsToday, stopped);
    }

    // ---- 1. who ------------------------------------------------------------------------------

    private async Task<List<EligibleUser>> SelectEligibleUsersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activeSince = now.AddDays(-options.Value.InactiveAfterDays);

        // The gate for "is this user paying" is this one predicate — the payment integration will
        // write ProEntitlements and need not touch the sweep.
        var profiles = await dbContext.UserJobSourceProfiles
            .Include(p => p.Queries)
            .Where(p => p.Enabled && p.Queries.Any())
            .Where(p => dbContext.ProEntitlements.Any(e => e.UserId == p.UserId && e.RevokedAt == null && e.ActiveUntil > now))
            .Where(p => dbContext.CvDocuments.Any(c => c.UserId == p.UserId))
            .Where(p => dbContext.RefreshTokens.Any(t => t.UserId == p.UserId && t.CreatedAt >= activeSince))
            .ToListAsync(cancellationToken);

        var userIds = profiles.Select(p => p.UserId).ToList();
        var limits = await dbContext.UserJobSourceSettings
            .Where(s => userIds.Contains(s.UserId) && s.WeeklyPostingLimit != null)
            .ToDictionaryAsync(s => s.UserId, s => s.WeeklyPostingLimit!.Value, cancellationToken);

        return profiles
            .Select(p => new EligibleUser(p, limits.GetValueOrDefault(p.UserId, options.Value.DefaultWeeklyPostingsPerUser)))
            .ToList();
    }

    // ---- 2. fetch ----------------------------------------------------------------------------

    /// <returns>True when the source told us to stop.</returns>
    private async Task<bool> RunQueryAsync(JobSourceQuery query, BudgetState budget, CancellationToken cancellationToken)
    {
        var seen = 0;
        var okPages = 0;
        var stopped = false;
        var outcome = "Ok";

        for (var page = 0; page < options.Value.MaxPagesPerQuery; page++)
        {
            var now = _timeProvider.GetUtcNow();
            if (!budget.CanSpend(now))
            {
                outcome = "BudgetExhausted";
                break;
            }

            await PauseAsync(budget, cancellationToken);
            var result = await client.SearchAsync(query, page * LinkedInJobSearchUrlBuilder.PageSize, cancellationToken);
            await RecordFetchAsync(JobSourceFetchKind.Search, result.Outcome, result.StatusCode, result.DurationMs, budget, cancellationToken);

            if (!result.IsOk)
            {
                outcome = result.Outcome.ToString();
                stopped = result.StopsSweep;
                break;
            }

            var cards = result.Value!;
            await UpsertCardsAsync(query, cards, page * LinkedInJobSearchUrlBuilder.PageSize, _timeProvider.GetUtcNow(), cancellationToken);
            seen += cards.Count;
            okPages++;

            if (cards.Count < LinkedInJobSearchUrlBuilder.PageSize)
            {
                break;
            }
        }

        // A query that got at least one page is done for the share window, even if the budget or
        // the source cut it short — what it got is what its users get this week. One that got
        // nothing is left unmarked so the next run tries it first.
        if (okPages > 0)
        {
            query.RecordRun(seen, outcome, _timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return stopped;
    }

    private async Task UpsertCardsAsync(JobSourceQuery query, IReadOnlyList<JobSourceCard> cards, int rankOffset, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = cards.Select(c => c.ExternalId).Distinct().ToList();
        var postings = await dbContext.JobSourcePostings
            .Where(p => p.Source == Source.LinkedIn && ids.Contains(p.ExternalId))
            .ToDictionaryAsync(p => p.ExternalId, cancellationToken);
        var postingIds = postings.Values.Select(p => p.Id).ToList();
        var links = await dbContext.JobSourceQueryPostings
            .Where(l => l.QueryId == query.Id && postingIds.Contains(l.PostingId))
            .ToDictionaryAsync(l => l.PostingId, cancellationToken);

        var rank = rankOffset;
        foreach (var card in cards.DistinctBy(c => c.ExternalId))
        {
            if (!postings.TryGetValue(card.ExternalId, out var posting))
            {
                posting = JobSourcePosting.Create(Source.LinkedIn, card.ExternalId, card.Title, card.CompanyName,
                    card.CompanyProfileUrl, card.Location, card.PostedAt, card.Url, now);
                dbContext.JobSourcePostings.Add(posting);
                postings[card.ExternalId] = posting;
            }
            else
            {
                posting.SeenAgain(card.Title, card.CompanyName, card.CompanyProfileUrl, card.Location, card.PostedAt, now);
            }

            if (links.TryGetValue(posting.Id, out var link))
            {
                link.SeenAgain(rank, now);
            }
            else
            {
                dbContext.JobSourceQueryPostings.Add(JobSourceQueryPosting.Create(query.Id, posting.Id, rank, now));
            }

            rank++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- 3. deliver --------------------------------------------------------------------------

    private async Task<int> DeliverAsync(EligibleUser user, int weekKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var userId = user.Profile.UserId;
        var queryIds = user.Profile.OrderedQueries.Select(q => q.QueryId).ToList();
        var since = now.AddDays(-options.Value.QueryShareDays);

        var surfaced = await dbContext.JobSourceQueryPostings
            .Where(l => queryIds.Contains(l.QueryId) && l.LastSeenAt >= since)
            .Join(dbContext.JobSourcePostings, l => l.PostingId, p => p.Id,
                (l, p) => new Candidate(l.QueryId, p.Id, p.ExternalId, l.Rank))
            .ToListAsync(cancellationToken);

        // Round-robin across the user's titles, best rank first within each: a user with three
        // titles gets the top of each list, not fifty of the first.
        var candidates = RoundRobin(queryIds, surfaced);
        var candidateIds = candidates.Select(c => c.PostingId).ToList();

        var applied = await LoadAppliedLinkedInIdsAsync(userId, cancellationToken);
        var alreadyDelivered = await dbContext.UserJobSourceDeliveries
            .Where(d => d.UserId == userId && candidateIds.Contains(d.PostingId))
            .Select(d => d.PostingId)
            .ToHashSetAsync(cancellationToken);
        var deliveredThisWeek = await dbContext.UserJobSourceDeliveries
            .CountAsync(d => d.UserId == userId && d.WeekKey == weekKey, cancellationToken);

        var excludedApplied = candidates.Count(c => applied.Contains(c.ExternalId));
        var excludedShown = candidates.Count(c => !applied.Contains(c.ExternalId) && alreadyDelivered.Contains(c.PostingId));
        var room = Math.Max(0, user.WeeklyPostingLimit - deliveredThisWeek);
        var toDeliver = candidates
            .Where(c => !applied.Contains(c.ExternalId) && !alreadyDelivered.Contains(c.PostingId))
            .Take(room)
            .ToList();

        foreach (var (candidate, index) in toDeliver.Select((c, i) => (c, i)))
        {
            dbContext.UserJobSourceDeliveries.Add(
                UserJobSourceDelivery.Create(userId, candidate.PostingId, candidate.QueryId, weekKey, deliveredThisWeek + index, now));
        }

        var run = await dbContext.UserJobSourceRuns.FindAsync([userId, weekKey], cancellationToken);
        if (run is null)
        {
            run = UserJobSourceRun.Create(userId, weekKey, now);
            dbContext.UserJobSourceRuns.Add(run);
        }

        run.Record(candidates.Count, toDeliver.Count, excludedApplied, excludedShown, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return toDeliver.Count;
    }

    private static List<Candidate> RoundRobin(IReadOnlyList<Guid> queryOrder, List<Candidate> surfaced)
    {
        var lanes = queryOrder
            .Select(id => surfaced.Where(c => c.QueryId == id).OrderBy(c => c.Rank).ToList())
            .ToList();
        var result = new List<Candidate>();
        var seen = new HashSet<Guid>();
        for (var i = 0; lanes.Any(l => i < l.Count); i++)
        {
            foreach (var lane in lanes)
            {
                if (i < lane.Count && seen.Add(lane[i].PostingId))
                {
                    result.Add(lane[i]);
                }
            }
        }

        return result;
    }

    /// <summary>Every LinkedIn posting id the user has an application for — from the application's
    /// own URL, from the shared job's external id, and from the shared job's URL, because which of
    /// the three carries it depends on how the application was created.</summary>
    private async Task<HashSet<string>> LoadAppliedLinkedInIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var applications = await dbContext.Applications
            .Where(a => a.UserId == userId)
            .Select(a => new { a.JobUrl, a.JobId })
            .ToListAsync(cancellationToken);
        var jobIds = applications.Where(a => a.JobId != null).Select(a => a.JobId!.Value).Distinct().ToList();
        var jobs = await dbContext.Jobs
            .Where(j => jobIds.Contains(j.Id))
            .Select(j => new { j.Source, j.ExternalId, j.Url })
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in applications.Select(a => LinkedInJobIdExtractor.Extract(a.JobUrl)))
        {
            if (id is not null) ids.Add(id);
        }

        foreach (var job in jobs)
        {
            if (job.Source == Source.LinkedIn && !string.IsNullOrEmpty(job.ExternalId)) ids.Add(job.ExternalId);
            if (LinkedInJobIdExtractor.Extract(job.Url) is { } id) ids.Add(id);
        }

        return ids;
    }

    // ---- 4. details --------------------------------------------------------------------------

    private async Task<int> FetchDetailsAsync(int weekKey, BudgetState budget, CancellationToken cancellationToken)
    {
        // Most-delivered first: a posting three users got is worth the request more than one one user got.
        var pending = await dbContext.UserJobSourceDeliveries
            .Where(d => d.WeekKey == weekKey)
            .Join(dbContext.JobSourcePostings.Where(p => p.DetailFetchedAt == null), d => d.PostingId, p => p.Id, (d, p) => p)
            .GroupBy(p => p.Id)
            .Select(g => new { PostingId = g.Key, Deliveries = g.Count() })
            .OrderByDescending(x => x.Deliveries)
            .Select(x => x.PostingId)
            .ToListAsync(cancellationToken);

        var fetched = 0;
        foreach (var postingId in pending)
        {
            if (!budget.CanSpend(_timeProvider.GetUtcNow()))
            {
                break;
            }

            var posting = await dbContext.JobSourcePostings.SingleAsync(p => p.Id == postingId, cancellationToken);
            await PauseAsync(budget, cancellationToken);
            var result = await client.GetPostingAsync(posting.ExternalId, cancellationToken);
            await RecordFetchAsync(JobSourceFetchKind.Detail, result.Outcome, result.StatusCode, result.DurationMs, budget, cancellationToken);

            if (result.StopsSweep)
            {
                break;
            }

            if (result.IsOk)
            {
                var detail = result.Value!;
                posting.SetDetail(detail.Description, detail.Seniority, detail.EmploymentType, detail.JobFunction, detail.Industries,
                    _timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
                fetched++;
            }
        }

        return fetched;
    }

    // ---- 5. forget ---------------------------------------------------------------------------

    private async Task PruneAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-options.Value.RetentionDays);
        await dbContext.JobSourcePostings
            .Where(p => p.LastSeenAt < cutoff && !dbContext.UserJobSourceDeliveries.Any(d => d.PostingId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.JobSourceFetches
            .Where(f => f.At < now.AddDays(-LedgerRetentionDays))
            .ExecuteDeleteAsync(cancellationToken);
    }

    // ---- budget & ledger ---------------------------------------------------------------------

    private async Task<BudgetState> LoadBudgetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var requestsToday = await dbContext.JobSourceFetches.CountAsync(f => f.At >= dayStart, cancellationToken);
        var lastBlockedAt = await dbContext.JobSourceFetches
            .Where(f => f.Outcome == JobSourceFetchOutcome.Blocked || f.Outcome == JobSourceFetchOutcome.RateLimited)
            .MaxAsync(f => (DateTimeOffset?)f.At, cancellationToken);
        return new BudgetState(requestsToday, lastBlockedAt, options.Value.MaxRequestsPerDay,
            TimeSpan.FromHours(options.Value.CircuitCooldownHours));
    }

    private async Task RecordFetchAsync(JobSourceFetchKind kind, JobSourceFetchOutcome outcome, int? statusCode, int durationMs,
        BudgetState budget, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        dbContext.JobSourceFetches.Add(JobSourceFetch.Create(Source.LinkedIn, kind, statusCode, outcome, durationMs, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        budget.Spent(outcome, now);
        if (outcome is JobSourceFetchOutcome.Blocked or JobSourceFetchOutcome.RateLimited)
        {
            logger.LogWarning("Job source {Kind} fetch answered {Outcome} ({StatusCode}); the sweep stops until {CooldownUntil}",
                kind, outcome, statusCode, budget.CooldownUntil);
        }
    }

    private async Task PauseAsync(BudgetState budget, CancellationToken cancellationToken)
    {
        var min = options.Value.MinDelayMs;
        if (budget.RequestsThisRun > 0 && min > 0)
        {
            await Task.Delay(min + Random.Shared.Next(min + 1), cancellationToken);
        }
    }

    private sealed record EligibleUser(UserJobSourceProfile Profile, int WeeklyPostingLimit);

    private sealed record Candidate(Guid QueryId, Guid PostingId, string ExternalId, int Rank);

    private sealed class BudgetState(int requestsToday, DateTimeOffset? lastBlockedAt, int maxRequestsPerDay, TimeSpan cooldown)
    {
        public int RequestsToday { get; private set; } = requestsToday;

        public int RequestsThisRun { get; private set; }

        public DateTimeOffset? LastBlockedAt { get; private set; } = lastBlockedAt;

        public DateTimeOffset? CooldownUntil => JobSourceBudget.CooldownUntil(LastBlockedAt, cooldown);

        public bool CanSpend(DateTimeOffset now) => JobSourceBudget.CanSpend(RequestsToday, maxRequestsPerDay, LastBlockedAt, cooldown, now);

        public bool InCooldown(DateTimeOffset now) => JobSourceBudget.IsInCooldown(LastBlockedAt, cooldown, now);

        public void Spent(JobSourceFetchOutcome outcome, DateTimeOffset now)
        {
            RequestsToday++;
            RequestsThisRun++;
            if (outcome is JobSourceFetchOutcome.Blocked or JobSourceFetchOutcome.RateLimited)
            {
                LastBlockedAt = now;
            }
        }
    }
}
