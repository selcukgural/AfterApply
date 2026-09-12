using System.Globalization;
using AfterApply.Application.Common;
using AfterApply.Application.JobSearch;
using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Domain.JobSearch;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// Everything between an endpoint and <see cref="IJSearchClient"/>: the user's effective settings,
/// the country/language rule, the shared tables (postings by id, cached answers by request), the
/// ledger, and the two ceilings. The order per call is fixed — resolve, normalise, look in our
/// own tables, check the ceilings, only then spend a credit — because on the BASIC plan a credit
/// is 0.5% of the month.
///
/// The ledger row is written on every path, first and on its own, so a later failure (the cache
/// write losing a race, the provider timing out) can never lose the count.
/// </summary>
internal sealed class JobSearchService(
    AppDbContext dbContext,
    IJSearchClient client,
    IJobSearchSettingsService settingsService,
    IOptions<JobSearchOptions> options,
    ILogger<JobSearchService> logger,
    TimeProvider? timeProvider = null) : IJobSearchService
{
    private const string NotConfiguredCode = "JOB_SEARCH_NOT_CONFIGURED";
    private const string RateLimitedCode = "JOB_SEARCH_UPSTREAM_RATE_LIMITED";
    private const string UpstreamErrorCode = "JOB_SEARCH_UPSTREAM_ERROR";
    private const string RejectedCode = "JOB_SEARCH_UPSTREAM_REJECTED";
    private const string QuotaExhaustedCode = "JOB_SEARCH_QUOTA_EXHAUSTED";
    private const string DailyLimitCode = "JOB_SEARCH_DAILY_LIMIT_REACHED";
    private const string TooManyIdsCode = "JOB_SEARCH_TOO_MANY_IDS";

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<JobSearchResultsResponse> SearchAsync(Guid userId, SearchJobsQuery query, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var settings = await settingsService.ResolveAsync(userId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var (country, language) = ResolveCountryAndLanguage(query.Country, query.Language, settings);
        var text = CollapseWhitespace(query.Query) ?? string.Empty;
        var cursor = Clean(query.Cursor);
        var location = CollapseWhitespace(query.Location) ?? settings.Location;
        var datePosted = query.DatePosted ?? settings.DatePosted;
        var workFromHome = query.WorkFromHome ?? settings.WorkFromHome;
        var numPages = Math.Clamp(query.NumPages ?? 1, 1, Math.Max(1, settings.MaxPagesPerSearch));
        JobSearchCsv.TryParseEnumList<JobSearchEmploymentType>(query.EmploymentTypes, out var employmentTypes, out _);
        JobSearchCsv.TryParseEnumList<JobSearchJobRequirement>(query.JobRequirements, out var jobRequirements, out _);
        var excludedPublishers = JobSearchCsv.Split(query.ExcludeJobPublishers);

        var canonical = JobSearchCacheKey.Canonical(JobSearchOperation.Search,
        [
            new("query", JobSearchCacheKey.NormalizeText(text)),
            new("cursor", cursor),
            new("num_pages", JobSearchCacheKey.Number(numPages)),
            new("country", country),
            new("language", language),
            new("location", JobSearchCacheKey.NormalizeText(location)),
            new("date_posted", datePosted == JobSearchDatePosted.All ? null : datePosted.ToWire()),
            new("work_from_home", workFromHome ? "true" : null),
            new("employment_types", JobSearchCacheKey.NormalizeList(employmentTypes.Select(e => e.ToWire()))),
            new("job_requirements", JobSearchCacheKey.NormalizeList(jobRequirements.Select(r => r.ToWire()))),
            new("radius", JobSearchCacheKey.Number(query.Radius)),
            new("exclude_job_publishers", JobSearchCacheKey.NormalizeList(excludedPublishers))
        ]);
        var keyHash = JobSearchCacheKey.Hash(canonical);
        var credits = numPages;

        var cached = await FindCacheEntryAsync(JobSearchOperation.Search, keyHash, cancellationToken);
        if (cached is not null && cached.IsFresh(now, JobSearchPayloadJson.SchemaVersion)
            && JobSearchPayloadJson.Deserialize<JobSearchResultsResponse>(cached.Payload) is { } hit)
        {
            await RecordCacheHitAsync(userId, JobSearchOperation.Search, now, cancellationToken);
            return hit with { Meta = new JobSearchMeta(true, cached.FetchedAt, 0) };
        }

        await EnsureQuotaAsync(userId, settings, credits, now, cancellationToken);

        JSearchResult<JSearchSearchData> result;
        try
        {
            // num_pages is the provider's default when 1, and like the other defaults it is not
            // sent — the cache key still carries it, so a one-page and a three-page fetch differ.
            result = await client.SearchAsync(new JSearchSearchRequest(
                text, cursor, numPages == 1 ? null : numPages, country, language, location, datePosted, workFromHome,
                employmentTypes, jobRequirements, query.Radius, excludedPublishers), cancellationToken);
        }
        catch (JSearchException exception)
        {
            await RecordFailureAsync(userId, JobSearchOperation.Search, credits, exception, now, cancellationToken);
            throw Translate(exception, now);
        }

        await RecordSuccessAsync(userId, JobSearchOperation.Search, credits, result.RequestId, result.RateLimit, now, result.Attempts, cancellationToken);

        var jobs = (result.Data.Jobs ?? [])
            .Where(j => !string.IsNullOrWhiteSpace(j.JobId))
            .Select(JobSearchMapper.ToSummary)
            .ToList();
        var response = new JobSearchResultsResponse(jobs, Clean(result.Data.Cursor), new JobSearchMeta(false, now, credits));

        await StoreAsync(async () =>
        {
            await UpsertSummariesAsync(jobs, country, now, cancellationToken);
            WriteCacheEntry(cached, JobSearchOperation.Search, keyHash, canonical,
                JobSearchPayloadJson.Serialize(response), result.RequestId, now,
                now.AddHours(options.Value.SearchCacheHours));
        }, cancellationToken);

        return response;
    }

    public async Task<JobSearchJobDetailsResponse> GetJobDetailsAsync(Guid userId, GetJobDetailsQuery query, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var settings = await settingsService.ResolveAsync(userId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var (country, language) = ResolveCountryAndLanguage(query.Country, query.Language, settings);
        var ids = JobSearchCsv.Split(query.Ids, ignoreCase: false);
        if (ids.Count > settings.MaxJobIdsPerDetails)
        {
            throw new CodedException(TooManyIdsCode, $"At most {settings.MaxJobIdsPerDetails} job ids per request.",
                settings.MaxJobIdsPerDetails);
        }

        // Id by id, not request by request: the second person to open a posting someone else
        // already opened pays nothing, and a batch only spends credits on the ids nobody has.
        var rows = await dbContext.JobSearchJobs
            .Where(j => j.Country == country && ids.Contains(j.JobId))
            .ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(r => r.JobId, StringComparer.Ordinal);

        var found = new Dictionary<string, JobSearchJobDetailResponse>(StringComparer.Ordinal);
        DateTimeOffset? oldestFetch = null;
        foreach (var row in rows)
        {
            if (row.HasFreshDetail(now, JobSearchPayloadJson.SchemaVersion)
                && JobSearchPayloadJson.Deserialize<JobSearchJobDetailResponse>(row.Detail!) is { } detail)
            {
                found[row.JobId] = detail;
                oldestFetch = oldestFetch is null || row.DetailFetchedAt < oldestFetch ? row.DetailFetchedAt : oldestFetch;
            }
        }

        var missing = ids.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count == 0)
        {
            await RecordCacheHitAsync(userId, JobSearchOperation.JobDetails, now, cancellationToken);
            return new JobSearchJobDetailsResponse(Order(ids, found), new JobSearchMeta(true, oldestFetch ?? now, 0));
        }

        var credits = missing.Count;
        await EnsureQuotaAsync(userId, settings, credits, now, cancellationToken);

        JSearchResult<IReadOnlyList<JSearchJob>> result;
        try
        {
            result = await client.GetJobDetailsAsync(new JSearchJobDetailsRequest(missing, country, language), cancellationToken);
        }
        catch (JSearchException exception)
        {
            await RecordFailureAsync(userId, JobSearchOperation.JobDetails, credits, exception, now, cancellationToken);
            throw Translate(exception, now);
        }

        await RecordSuccessAsync(userId, JobSearchOperation.JobDetails, credits, result.RequestId, result.RateLimit, now, result.Attempts, cancellationToken);

        var fetched = result.Data
            .Where(j => !string.IsNullOrWhiteSpace(j.JobId))
            .Select(JobSearchMapper.ToDetail)
            .ToList();
        foreach (var detail in fetched)
        {
            found[detail.JobId] = detail;
        }

        await StoreAsync(() =>
        {
            var expiresAt = now.AddHours(options.Value.JobDetailsCacheHours);
            foreach (var detail in fetched)
            {
                var json = JobSearchPayloadJson.Serialize(detail);
                if (byId.TryGetValue(detail.JobId, out var row))
                {
                    row.UpdateSummary(detail.Title, detail.EmployerName, detail.Publisher,
                        JobSearchPayloadJson.Serialize(ToSummary(detail)), JobSearchPayloadJson.SchemaVersion, now);
                    row.SetDetail(json, JobSearchPayloadJson.SchemaVersion, now, expiresAt);
                }
                else
                {
                    var created = JobSearchJob.CreateFromSummary(detail.JobId, country, detail.Title, detail.EmployerName,
                        detail.Publisher, JobSearchPayloadJson.Serialize(ToSummary(detail)), JobSearchPayloadJson.SchemaVersion, now);
                    created.SetDetail(json, JobSearchPayloadJson.SchemaVersion, now, expiresAt);
                    dbContext.JobSearchJobs.Add(created);
                    byId[detail.JobId] = created;
                }
            }

            return Task.CompletedTask;
        }, cancellationToken);

        return new JobSearchJobDetailsResponse(Order(ids, found), new JobSearchMeta(false, now, credits));
    }

    public Task<JobSearchSalaryEstimatesResponse> GetEstimatedSalaryAsync(Guid userId, EstimatedSalaryQuery query, CancellationToken cancellationToken)
    {
        var title = CollapseWhitespace(query.JobTitle) ?? string.Empty;
        var location = CollapseWhitespace(query.Location) ?? string.Empty;
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("job_title", JobSearchCacheKey.NormalizeText(title)),
            new("location", JobSearchCacheKey.NormalizeText(location)),
            new("location_type", query.LocationType == JobSearchLocationType.Any ? null : query.LocationType.ToWire()),
            new("years_of_experience", query.YearsOfExperience == JobSearchExperienceRange.All ? null : query.YearsOfExperience.ToWire())
        };

        return CachedLookupAsync(userId, JobSearchOperation.EstimatedSalary, parameters,
            async ct =>
            {
                var result = await client.GetEstimatedSalaryAsync(
                    new JSearchEstimatedSalaryRequest(title, location, query.LocationType, query.YearsOfExperience), ct);
                return (result.Data.Select(JobSearchMapper.ToSalaryEstimate).ToList(), result.RequestId, result.RateLimit, result.Attempts);
            },
            (estimates, meta) => new JobSearchSalaryEstimatesResponse(estimates, meta),
            cancellationToken);
    }

    public Task<JobSearchCompanySalariesResponse> GetCompanyJobSalaryAsync(Guid userId, CompanyJobSalaryQuery query, CancellationToken cancellationToken)
    {
        var company = CollapseWhitespace(query.Company) ?? string.Empty;
        var title = CollapseWhitespace(query.JobTitle) ?? string.Empty;
        var location = CollapseWhitespace(query.Location);
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("company", JobSearchCacheKey.NormalizeText(company)),
            new("job_title", JobSearchCacheKey.NormalizeText(title)),
            new("location", JobSearchCacheKey.NormalizeText(location)),
            new("location_type", query.LocationType == JobSearchLocationType.Any ? null : query.LocationType.ToWire()),
            new("years_of_experience", query.YearsOfExperience == JobSearchExperienceRange.All ? null : query.YearsOfExperience.ToWire())
        };

        return CachedLookupAsync(userId, JobSearchOperation.CompanyJobSalary, parameters,
            async ct =>
            {
                var result = await client.GetCompanyJobSalaryAsync(
                    new JSearchCompanySalaryRequest(company, title, location, query.LocationType, query.YearsOfExperience), ct);
                return (result.Data.Select(JobSearchMapper.ToCompanySalary).ToList(), result.RequestId, result.RateLimit, result.Attempts);
            },
            (salaries, meta) => new JobSearchCompanySalariesResponse(salaries, meta),
            cancellationToken);
    }

    public async Task<JobSearchUsageResponse> GetUsageAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await settingsService.ResolveAsync(userId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var dayStart = JobSearchQuotaWindows.DayStart(now);
        var monthStart = JobSearchQuotaWindows.MonthStart(now, options.Value.MonthlyResetDay);

        var dailyUsed = await dbContext.JobSearchUsages
            .Where(u => u.UserId == userId && u.RequestedAt >= dayStart)
            .SumAsync(u => u.Credits, cancellationToken);
        var monthlyUsed = await dbContext.JobSearchUsages
            .Where(u => u.RequestedAt >= monthStart)
            .SumAsync(u => u.Credits, cancellationToken);
        var latest = await dbContext.JobSearchUsages
            .Where(u => u.RequestedAt >= monthStart && u.UpstreamRequestsRemaining != null)
            .OrderByDescending(u => u.RequestedAt)
            .Select(u => new { u.UpstreamRequestsRemaining, u.RequestedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return new JobSearchUsageResponse(dailyUsed, settings.PerUserDailyCredits, monthlyUsed,
            options.Value.GlobalMonthlyCredits, monthStart, latest?.UpstreamRequestsRemaining, latest?.RequestedAt);
    }

    // ---- the shared skeleton for the two salary lookups -------------------------------------

    private async Task<TResponse> CachedLookupAsync<TItem, TResponse>(
        Guid userId,
        JobSearchOperation operation,
        IReadOnlyList<KeyValuePair<string, string?>> parameters,
        Func<CancellationToken, Task<(List<TItem> Items, string? RequestId, JSearchRateLimit RateLimit, int Attempts)>> fetch,
        Func<IReadOnlyList<TItem>, JobSearchMeta, TResponse> build,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        EnsureConfigured();
        var settings = await settingsService.ResolveAsync(userId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var canonical = JobSearchCacheKey.Canonical(operation, parameters);
        var keyHash = JobSearchCacheKey.Hash(canonical);
        const int credits = 1;

        var cached = await FindCacheEntryAsync(operation, keyHash, cancellationToken);
        if (cached is not null && cached.IsFresh(now, JobSearchPayloadJson.SchemaVersion)
            && JobSearchPayloadJson.Deserialize<CachedItems<TItem>>(cached.Payload) is { Items: not null } hit)
        {
            await RecordCacheHitAsync(userId, operation, now, cancellationToken);
            return build(hit.Items, new JobSearchMeta(true, cached.FetchedAt, 0));
        }

        await EnsureQuotaAsync(userId, settings, credits, now, cancellationToken);

        List<TItem> items;
        string? requestId;
        JSearchRateLimit rateLimit;
        int attempts;
        try
        {
            (items, requestId, rateLimit, attempts) = await fetch(cancellationToken);
        }
        catch (JSearchException exception)
        {
            await RecordFailureAsync(userId, operation, credits, exception, now, cancellationToken);
            throw Translate(exception, now);
        }

        await RecordSuccessAsync(userId, operation, credits, requestId, rateLimit, now, attempts, cancellationToken);

        await StoreAsync(() =>
        {
            WriteCacheEntry(cached, operation, keyHash, canonical,
                JobSearchPayloadJson.Serialize(new CachedItems<TItem>(items)), requestId, now,
                now.AddHours(options.Value.SalaryCacheHours));
            return Task.CompletedTask;
        }, cancellationToken);

        return build(items, new JobSearchMeta(false, now, credits));
    }

    /// <summary>The stored shape of a salary answer: the items alone. Meta is a property of the
    /// call, not of the answer, and is rebuilt on every read.</summary>
    private sealed record CachedItems<TItem>(List<TItem>? Items);

    // ---- settings, normalisation --------------------------------------------------------------

    private void EnsureConfigured()
    {
        if (!options.Value.IsConfigured)
        {
            throw new CodedException(NotConfiguredCode, "Job search is not configured.");
        }
    }

    /// <summary>
    /// Country: the request's, else the user's saved default, else the global one (tr). Language:
    /// the request's, else the user's saved default, else <b>nothing</b> — JSearch then uses the
    /// country's primary language, and an English-titled posting in Türkiye still comes back.
    /// Sending a language the country does not support returns no results at all, which is why
    /// no global default exists to fall back to.
    /// </summary>
    private static (string Country, string? Language) ResolveCountryAndLanguage(string? country, string? language,
        EffectiveJobSearchSettings settings) =>
        (JobSearchCacheKey.NormalizeCode(country) ?? settings.Country,
            JobSearchCacheKey.NormalizeCode(language) ?? settings.Language);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CollapseWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyList<JobSearchJobDetailResponse> Order(IReadOnlyList<string> ids,
        Dictionary<string, JobSearchJobDetailResponse> found) =>
        ids.Where(found.ContainsKey).Select(id => found[id]).ToList();

    private static JobSearchJobSummaryResponse ToSummary(JobSearchJobDetailResponse d) =>
        new(d.JobId, d.Title, d.EmployerName, d.EmployerLogo, d.EmployerWebsite, d.Publisher, d.EmploymentType,
            d.EmploymentTypes, d.ApplyLink, d.ApplyIsDirect, d.ApplyOptions, d.Description, d.IsRemote, d.PostedAtText,
            d.PostedAtTimestamp, d.PostedAtUtc, d.Location, d.City, d.State, d.Country, d.Latitude, d.Longitude,
            d.Benefits, d.BenefitLabels, d.GoogleLink, d.Salary, d.OnetSoc, d.OnetJobZone);

    // ---- ceilings -----------------------------------------------------------------------------

    /// <summary>
    /// User first, then the product: an account that has spent its day is told so without
    /// learning anything about the shared month. Both sums include failed attempts (the ledger
    /// charges them) and exclude cache hits (zero credits). Two calls that pass at the same
    /// instant both go through — the headroom in GlobalMonthlyCredits is for exactly that.
    /// </summary>
    private async Task EnsureQuotaAsync(Guid userId, EffectiveJobSearchSettings settings, int credits, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dayStart = JobSearchQuotaWindows.DayStart(now);
        var dailyUsed = await dbContext.JobSearchUsages
            .Where(u => u.UserId == userId && u.RequestedAt >= dayStart)
            .SumAsync(u => u.Credits, cancellationToken);
        if (dailyUsed + credits > settings.PerUserDailyCredits)
        {
            throw new CodedException(DailyLimitCode, "Daily job search limit reached.", settings.PerUserDailyCredits);
        }

        var monthStart = JobSearchQuotaWindows.MonthStart(now, options.Value.MonthlyResetDay);
        var monthlyUsed = await dbContext.JobSearchUsages
            .Where(u => u.RequestedAt >= monthStart)
            .SumAsync(u => u.Credits, cancellationToken);
        if (monthlyUsed + credits > options.Value.GlobalMonthlyCredits)
        {
            throw QuotaExhausted(now);
        }

        // The provider's own word beats our arithmetic: if its last answer said nothing is left,
        // nothing is left, whatever the ledger adds up to.
        var providerRemaining = await dbContext.JobSearchUsages
            .Where(u => u.RequestedAt >= monthStart && u.UpstreamRequestsRemaining != null)
            .OrderByDescending(u => u.RequestedAt)
            .Select(u => u.UpstreamRequestsRemaining)
            .FirstOrDefaultAsync(cancellationToken);
        if (providerRemaining == 0)
        {
            throw QuotaExhausted(now);
        }
    }

    private CodedException QuotaExhausted(DateTimeOffset now)
    {
        var resetsOn = JobSearchQuotaWindows.MonthEnd(now, options.Value.MonthlyResetDay);
        return new CodedException(QuotaExhaustedCode, "Monthly job search quota exhausted.",
            resetsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    // ---- ledger -------------------------------------------------------------------------------

    private Task RecordCacheHitAsync(Guid userId, JobSearchOperation operation, DateTimeOffset now, CancellationToken cancellationToken) =>
        RecordAsync(JobSearchUsage.Create(userId, operation, 0, cacheHit: true, succeeded: true, null, null, null, now), cancellationToken);

    /// <summary>Per attempt: a search that needed the one retry cost the provider two requests
    /// per page, and the meter confirmed it does bill both (2026-09-12).</summary>
    private Task RecordSuccessAsync(Guid userId, JobSearchOperation operation, int credits, string? requestId,
        JSearchRateLimit rateLimit, DateTimeOffset now, int attempts, CancellationToken cancellationToken) =>
        RecordAsync(JobSearchUsage.Create(userId, operation, credits * Math.Max(1, attempts), cacheHit: false, succeeded: true,
            200, requestId, rateLimit.Remaining, now), cancellationToken);

    /// <summary>Charged per attempt unless the gateway refused the key: the meter bills every
    /// request that reaches the provider, a timed-out one included.</summary>
    private Task RecordFailureAsync(Guid userId, JobSearchOperation operation, int credits, JSearchException exception,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var charged = exception.Failure is JSearchFailure.NotConfigured or JSearchFailure.Unauthorized
            ? 0
            : credits * Math.Max(1, exception.Attempts);
        var remaining = exception.Data["remaining"] as int?;
        return RecordAsync(JobSearchUsage.Create(userId, operation, charged, cacheHit: false, succeeded: false,
            exception.StatusCode, exception.RequestId, remaining, now), cancellationToken);
    }

    private async Task RecordAsync(JobSearchUsage usage, CancellationToken cancellationToken)
    {
        dbContext.JobSearchUsages.Add(usage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- shared tables ------------------------------------------------------------------------

    private Task<JobSearchCacheEntry?> FindCacheEntryAsync(JobSearchOperation operation, string keyHash, CancellationToken cancellationToken) =>
        dbContext.JobSearchCacheEntries.FirstOrDefaultAsync(e => e.Operation == operation && e.KeyHash == keyHash, cancellationToken);

    private void WriteCacheEntry(JobSearchCacheEntry? existing, JobSearchOperation operation, string keyHash, string canonical,
        string payload, string? requestId, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (existing is not null)
        {
            existing.Refresh(payload, JobSearchPayloadJson.SchemaVersion, requestId, now, expiresAt);
            return;
        }

        dbContext.JobSearchCacheEntries.Add(JobSearchCacheEntry.Create(operation, keyHash, canonical, payload,
            JobSearchPayloadJson.SchemaVersion, requestId, now, expiresAt));
    }

    private async Task UpsertSummariesAsync(IReadOnlyList<JobSearchJobSummaryResponse> jobs, string country, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (jobs.Count == 0)
        {
            return;
        }

        var ids = jobs.Select(j => j.JobId).Distinct(StringComparer.Ordinal).ToList();
        var existing = await dbContext.JobSearchJobs
            .Where(j => j.Country == country && ids.Contains(j.JobId))
            .ToDictionaryAsync(j => j.JobId, StringComparer.Ordinal, cancellationToken);

        foreach (var job in jobs)
        {
            var json = JobSearchPayloadJson.Serialize(job);
            if (existing.TryGetValue(job.JobId, out var row))
            {
                row.UpdateSummary(job.Title, job.EmployerName, job.Publisher, json, JobSearchPayloadJson.SchemaVersion, now);
            }
            else
            {
                var created = JobSearchJob.CreateFromSummary(job.JobId, country, job.Title, job.EmployerName, job.Publisher,
                    json, JobSearchPayloadJson.SchemaVersion, now);
                dbContext.JobSearchJobs.Add(created);
                existing[job.JobId] = created;
            }
        }
    }

    /// <summary>
    /// Writes the shared tables after the ledger is already safe. A unique-index collision means
    /// a concurrent caller stored the same answer first — theirs is as good as ours, so the loss
    /// is logged and the response still goes out. Also sweeps cache rows a day past expiry: at
    /// most a few hundred rows a month by construction, so a job scheduler would be more moving
    /// parts than the problem has.
    /// </summary>
    private async Task StoreAsync(Func<Task> write, CancellationToken cancellationToken)
    {
        try
        {
            await write();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogInformation("Job search cache write lost a race with a concurrent caller; the response is unaffected.");
            dbContext.ChangeTracker.Clear();
            return;
        }
        catch (DbUpdateException exception)
        {
            // Anything else is a real defect (a value wider than its column, say) — the answer
            // still goes out, but this must not read as a benign race in the logs. Seen once:
            // provider ids at 402 characters against a 200-character column, 2026-09-12.
            logger.LogError(exception, "Job search store write failed; the response is unaffected but nothing was stored.");
            dbContext.ChangeTracker.Clear();
            return;
        }

        var sweepBefore = _timeProvider.GetUtcNow().AddDays(-1);
        await dbContext.JobSearchCacheEntries
            .Where(e => e.ExpiresAt < sweepBefore)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // ---- errors -------------------------------------------------------------------------------

    private CodedException Translate(JSearchException exception, DateTimeOffset now) => exception.Failure switch
    {
        JSearchFailure.NotConfigured or JSearchFailure.Unauthorized =>
            new CodedException(NotConfiguredCode, exception.Message),
        JSearchFailure.RateLimited when exception.Data["remaining"] is 0 => QuotaExhausted(now),
        JSearchFailure.RateLimited => new CodedException(RateLimitedCode, exception.Message),
        JSearchFailure.BadRequest => new CodedException(RejectedCode, exception.Message),
        _ => new CodedException(UpstreamErrorCode, exception.Message)
    };
}
