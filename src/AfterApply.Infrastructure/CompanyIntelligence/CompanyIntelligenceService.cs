using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Application.ResponseRates;
using AfterApply.Application.SilenceReports;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using AfterApply.Infrastructure.SilenceReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyIntelligence;

internal sealed class CompanyIntelligenceService(
    AppDbContext dbContext,
    ISectorResponseRateService sectorResponseRates,
    IOptions<CompanyIntelligenceOptions> options,
    IOptions<SilenceReportOptions> silenceReportOptions)
    : ICompanyIntelligenceService
{
    public async Task<CompanyIntelligenceResponse?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Id, c.Name, c.Industry })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var opts = options.Value;
        var thresholds = new CompanyIntelligenceThresholds(
            opts.HiddenBelow, opts.MaturityDays, (int)Math.Round(opts.MaxContributorShare * 100));

        // Windowed on AppliedAt, which keeps the sample a clean cohort — "applications submitted in
        // this period" — rather than mixing an old application with a recent status change. See
        // CompanyIntelligenceOptions.WindowMonths for why an unbounded aggregate is the unfair one.
        var windowEnd = DateTimeOffset.UtcNow;
        var windowStart = windowEnd.AddMonths(-opts.WindowMonths);

        // The sector's aggregate names nobody, so it travels with the answer whether or not the
        // company itself clears its threshold — see CompanySectorComparison.
        var sector = IndustrySectorClassifier.Classify(company.Industry);
        var comparison = sector is null
            ? null
            : new CompanySectorComparison(sector.Value, await sectorResponseRates.GetSectorFiguresAsync(sector.Value, cancellationToken));

        // No UserId filter — unlike AnalyticsService, this aggregates across ALL users.
        var applications = await dbContext.Applications
            .Where(a => a.CompanyId == companyId && a.AppliedAt >= windowStart && a.AppliedAt <= windowEnd)
            .Select(a => new
            {
                a.Id, a.UserId, a.Status, a.AppliedAt,
                a.PromisedReplyBy, a.PromisedReplySince, a.RejectionNotice
            })
            .ToListAsync(cancellationToken);

        var (silenceReports, silenceThresholds) = await SilenceReportsAsync(companyId, windowEnd, cancellationToken);

        var total = applications.Count;
        var confidence = CompanyIntelligenceCalculations.ClassifyConfidence(
            total, opts.HiddenBelow, opts.VeryLowBelow, opts.LowBelow, opts.MediumBelow);

        if (confidence == ConfidenceBucket.Hidden)
        {
            // Defense in depth: don't even run the history join/grouping below — no
            // per-application response-time data is pulled into memory for a below-threshold
            // company.
            return Hidden();
        }

        // ApplicationStatusHistory has no navigation back to Application (ApplicationConfiguration:
        // HasMany(...).WithOne() with no inverse configured), so the join must be explicit rather
        // than h.Application — same as AnalyticsService.
        var applicationIds = applications.Select(a => a.Id).ToList();
        var history = await dbContext.ApplicationStatusHistories
            .Where(h => applicationIds.Contains(h.ApplicationId))
            .Select(h => new { h.ApplicationId, h.ToStatus, h.ChangedAt })
            .ToListAsync(cancellationToken);
        var historyByApplication = history
            .GroupBy(h => h.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.ToStatus, h.ChangedAt)).ToList());

        var samples = applications
            .Select(a => ResponseRateAggregator.ToSample(a.Id, a.UserId, a.Status, a.AppliedAt,
                historyByApplication.GetValueOrDefault(a.Id) ?? [],
                a.PromisedReplyBy, a.PromisedReplySince, a.RejectionNotice, windowEnd))
            .ToList();
        var figures = ResponseRateAggregator.Compute(samples, windowEnd, opts.MaturityDays);

        // The count cleared the ladder but the people did not: one person's history with a
        // company name on it. Indistinguishable from the count case by design.
        if (figures.MaxContributorShare > opts.MaxContributorShare)
        {
            return Hidden();
        }

        var responseTimeScore = CompanyIntelligenceCalculations.CalculateResponseTimeScore(
            figures.AverageFirstReplyDays, opts.ResponseTimeCapDays);
        var candidateExperienceScore = CompanyIntelligenceCalculations.CalculateCandidateExperienceScore(
            figures.ResponseRate, responseTimeScore, figures.ClosureRate,
            opts.ResponsivenessWeight, opts.ResponseTimeWeight, opts.ClosureRateWeight);

        var metrics = new CompanyIntelligenceMetrics(
            TotalApplications: figures.TotalApplications,
            MatureApplications: figures.MatureApplications,
            DistinctContributors: figures.DistinctContributors,
            ResponseRate: figures.ResponseRate,
            GhostingRate: figures.GhostingRate,
            InterviewRate: figures.InterviewRate,
            OfferRate: figures.OfferRate,
            PostInterviewSilenceRate: figures.PostInterviewSilenceRate,
            AverageResponseTimeDays: figures.AverageFirstReplyDays,
            MedianResponseTimeDays: figures.MedianFirstReplyDays,
            ClosureRate: figures.ClosureRate,
            CandidateExperienceScore: candidateExperienceScore,
            PromiseKeptRate: figures.PromiseKeptRate,
            RejectionNoticeRate: figures.RejectionNoticeRate);

        return new CompanyIntelligenceResponse(company.Id, company.Name, confidence,
            windowStart, windowEnd, metrics, comparison, thresholds, silenceReports, silenceThresholds);

        CompanyIntelligenceResponse Hidden() => new(company.Id, company.Name, ConfidenceBucket.Hidden,
            windowStart, windowEnd, Metrics: null, comparison, thresholds, silenceReports, silenceThresholds);
    }

    /// <summary>The company's anonymous reports over their own window, summarised only above their
    /// own floor. Stage and month only are read — nothing else in the row is needed to count.</summary>
    private async Task<(CompanySilenceReports?, SilenceReportThresholds)> SilenceReportsAsync(
        Guid companyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var opts = silenceReportOptions.Value;
        var thresholds = new SilenceReportThresholds(opts.MinimumReports, opts.MinimumQuarters, opts.WindowMonths);
        var since = now.AddMonths(-opts.WindowMonths);

        var rows = await dbContext.SilenceReports
            .Where(r => r.CompanyId == companyId && r.SubmittedAt >= since)
            .Select(r => new { r.Stage, r.SilentSinceMonth })
            .ToListAsync(cancellationToken);

        var summary = SilenceReportCalculations.Summarize(
            rows.Select(r => (r.Stage, r.SilentSinceMonth)).ToList(), opts.MinimumReports, opts.MinimumQuarters);
        return (summary, thresholds);
    }
}
