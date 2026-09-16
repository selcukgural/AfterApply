using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Ai;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The scoring half of the weekly run. For every user who consented and has unscored deliveries
/// this week with a description: read the CV once, score each posting with one model call,
/// write the score on the delivery row and the tokens in the ledger. Three ceilings, checked
/// before every call: per user per week, per day across users, and the month's spend at list
/// prices. A call that fails counts as an attempt so a row is never re-bought more than
/// <see cref="Domain.JobSources.UserJobSourceDelivery.MaxScoreAttempts"/> times.
///
/// Idempotent within a week the same way the sweep is: a scored row is skipped, so a second run
/// picks up only what the first left — the row whose call failed, the posting whose description
/// arrived late. Logs carry counts only, never a CV or a posting.
/// </summary>
internal sealed class JobFitScoringService(
    AppDbContext dbContext,
    IJobFitScoringProvider provider,
    IUserCvTextReader cvTextReader,
    IOptions<JobSourceOptions> options,
    ILogger<JobFitScoringService> logger,
    TimeProvider? timeProvider = null) : IJobFitScoringService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> ScoreWeekAsync(int weekKey, CancellationToken cancellationToken)
    {
        var settings = options.Value.Scoring;
        if (!options.Value.Enabled || !settings.Enabled)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            logger.LogWarning("Job fit scoring skipped: JobSources:Scoring:ProjectId is not set");
            return 0;
        }

        var now = _timeProvider.GetUtcNow();
        var budget = await LoadBudgetAsync(now, cancellationToken);
        if (!budget.CanSpend)
        {
            logger.LogWarning("Job fit scoring skipped: {CallsToday} calls today (max {MaxCallsPerDay}), {CostThisMonth} USD this month (budget {Budget})",
                budget.CallsToday, settings.MaxCallsPerDay, budget.CostThisMonth, settings.MonthlyBudgetUsd);
            return 0;
        }

        // Users with something to score: consented, and holding a delivery this week that is
        // unscored, still has attempts left, and whose posting has a description to score against.
        var pending = await dbContext.UserJobSourceDeliveries
            .Where(d => d.WeekKey == weekKey && d.Score == null && d.ScoreAttempts < Domain.JobSources.UserJobSourceDelivery.MaxScoreAttempts)
            .Where(d => dbContext.JobSourcePostings.Any(p => p.Id == d.PostingId && p.Description != null))
            .Where(d => dbContext.UserJobSourceProfiles.Any(p => p.UserId == d.UserId && p.AiScoringConsentAcceptedAt != null))
            .GroupBy(d => d.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var scored = 0;
        foreach (var user in pending)
        {
            if (!budget.CanSpend)
            {
                break;
            }

            scored += await ScoreUserAsync(user.UserId, weekKey, budget, cancellationToken);
        }

        logger.LogInformation("Job fit scoring: {Users} users, {Scored} postings scored, {CallsToday} calls today, {CostThisMonth} USD this month",
            pending.Count, scored, budget.CallsToday, budget.CostThisMonth);
        return scored;
    }

    private async Task<int> ScoreUserAsync(Guid userId, int weekKey, Budget budget, CancellationToken cancellationToken)
    {
        var settings = options.Value.Scoring;
        var scoredThisWeek = await dbContext.UserJobSourceDeliveries
            .CountAsync(d => d.UserId == userId && d.WeekKey == weekKey && d.Score != null, cancellationToken);
        var room = settings.MaxPerUserPerWeek - scoredThisWeek;
        if (room <= 0)
        {
            return 0;
        }

        var cvText = await cvTextReader.ReadDefaultCvTextAsync(userId, cancellationToken);
        if (cvText is null)
        {
            return 0;
        }

        var locale = await dbContext.Users.Where(u => u.Id == userId).Select(u => u.PreferredLanguage).FirstOrDefaultAsync(cancellationToken) ?? "tr";

        // Best rank first, so if the budget runs out mid-user the postings the sweep put at the
        // top of the list are the ones that got a score.
        var rows = await dbContext.UserJobSourceDeliveries
            .Where(d => d.UserId == userId && d.WeekKey == weekKey && d.Score == null &&
                        d.ScoreAttempts < Domain.JobSources.UserJobSourceDelivery.MaxScoreAttempts)
            .Join(dbContext.JobSourcePostings.Where(p => p.Description != null), d => d.PostingId, p => p.Id, (d, p) => new { d, p })
            .OrderBy(x => x.d.Rank)
            .Take(room)
            .ToListAsync(cancellationToken);

        var scored = 0;
        foreach (var row in rows)
        {
            if (!budget.CanSpend)
            {
                break;
            }

            var request = new JobFitScoringRequest(cvText, row.p.Title, row.p.CompanyName, row.p.Location, row.p.Description!,
                row.p.Seniority, row.p.EmploymentType, locale);

            JobFitScoringResult? result;
            try
            {
                result = JobFitScores.Sanitize(await provider.ScoreAsync(request, cancellationToken));
            }
            catch (JobFitScoringProviderException exception)
            {
                // The status, never the body — see VertexGenerateContentClient.
                logger.LogWarning("Job fit scoring call failed: {Message}", exception.Message);
                result = null;
            }

            var now = _timeProvider.GetUtcNow();
            if (result is null)
            {
                row.d.RecordScoringAttemptFailed();
                Record(userId, 0, 0, succeeded: false, now, budget);
            }
            else
            {
                row.d.SetScore(result.Score, result.Summary, result.MatchedCriteria, result.MissingCriteria, result.RequiredSkills, now);
                Record(userId, result.InputTokens, result.OutputTokens, succeeded: true, now, budget);
                scored++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return scored;
    }

    private void Record(Guid userId, int inputTokens, int outputTokens, bool succeeded, DateTimeOffset now, Budget budget)
    {
        dbContext.AiUsageEntries.Add(AiUsageEntry.Create(userId, AiFeature.JobFitScoring, provider.Model, inputTokens, outputTokens, succeeded, now));
        budget.Spent(inputTokens, outputTokens);
    }

    private async Task<Budget> LoadBudgetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = options.Value.Scoring;
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var callsToday = await dbContext.AiUsageEntries.CountAsync(e => e.Feature == AiFeature.JobFitScoring && e.At >= dayStart, cancellationToken);
        var month = await dbContext.AiUsageEntries
            .Where(e => e.Feature == AiFeature.JobFitScoring && e.At >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new { Input = g.Sum(e => (long)e.InputTokens), Output = g.Sum(e => (long)e.OutputTokens) })
            .FirstOrDefaultAsync(cancellationToken);
        return new Budget(callsToday, month?.Input ?? 0, month?.Output ?? 0, settings);
    }

    /// <summary>Today's calls and the month's tokens, advanced in memory as the run spends so the
    /// ceilings hold within a run and not only between runs.</summary>
    private sealed class Budget(int callsToday, long inputTokensThisMonth, long outputTokensThisMonth, JobFitScoringSettings settings)
    {
        public int CallsToday { get; private set; } = callsToday;

        public long InputTokensThisMonth { get; private set; } = inputTokensThisMonth;

        public long OutputTokensThisMonth { get; private set; } = outputTokensThisMonth;

        public decimal CostThisMonth => JobFitScoringCost.Estimate(InputTokensThisMonth, OutputTokensThisMonth, settings);

        public bool CanSpend => CallsToday < settings.MaxCallsPerDay && CostThisMonth < settings.MonthlyBudgetUsd;

        public void Spent(int inputTokens, int outputTokens)
        {
            CallsToday++;
            InputTokensThisMonth += inputTokens;
            OutputTokensThisMonth += outputTokens;
        }
    }
}

public static class JobFitScoringCost
{
    /// <summary>Tokens to dollars at the configured list prices, rounded to the cent's tenth.</summary>
    public static decimal Estimate(long inputTokens, long outputTokens, JobFitScoringSettings settings) =>
        Math.Round(inputTokens / 1_000_000m * settings.InputUsdPerMillionTokens +
                   outputTokens / 1_000_000m * settings.OutputUsdPerMillionTokens, 3);
}
