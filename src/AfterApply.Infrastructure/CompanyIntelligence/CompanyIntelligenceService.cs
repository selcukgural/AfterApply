using AfterApply.Application.Analytics;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.CompanyIntelligence;

internal sealed class CompanyIntelligenceService(AppDbContext dbContext, IOptions<CompanyIntelligenceOptions> options)
    : ICompanyIntelligenceService
{
    public async Task<CompanyIntelligenceResponse?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        // No UserId filter — unlike AnalyticsService, this aggregates across ALL users.
        var applications = await dbContext.Applications
            .Where(a => a.CompanyId == companyId)
            .Select(a => new { a.Id, a.Status, a.AppliedAt })
            .ToListAsync(cancellationToken);

        var total = applications.Count;
        var opts = options.Value;
        var confidence = CompanyIntelligenceCalculations.ClassifyConfidence(
            total, opts.HiddenBelow, opts.VeryLowBelow, opts.LowBelow, opts.MediumBelow);

        if (confidence == ConfidenceBucket.Hidden)
        {
            // Defense in depth: don't even run the history join/grouping below — no
            // per-application response-time data is pulled into memory for a below-threshold
            // company.
            return new CompanyIntelligenceResponse(company.Id, company.Name, confidence, Metrics: null);
        }

        // ApplicationStatusHistory has no navigation back to Application (ApplicationConfiguration:
        // HasMany(...).WithOne() with no inverse configured), so the join must be explicit rather
        // than h.Application — same as AnalyticsService.
        var historyRows = await dbContext.ApplicationStatusHistories
            .Join(dbContext.Applications.Where(a => a.CompanyId == companyId),
                h => h.ApplicationId, a => a.Id,
                (h, a) => new { h.ApplicationId, h.ToStatus, h.ChangedAt, a.AppliedAt })
            .OrderBy(x => x.ChangedAt)
            .ToListAsync(cancellationToken);

        var respondedCount = 0;
        var interviewCount = 0;
        var offerCount = 0;
        var responseTimeDays = new List<double>();

        foreach (var group in historyRows.GroupBy(x => x.ApplicationId))
        {
            var firstResponse = group
                .Where(x => ApplicationStatusClassification.RespondedStatuses.Contains(x.ToStatus))
                .OrderBy(x => x.ChangedAt)
                .FirstOrDefault();

            if (firstResponse is not null)
            {
                respondedCount++;
                responseTimeDays.Add((firstResponse.ChangedAt - firstResponse.AppliedAt).TotalDays);
            }

            if (group.Any(x => ApplicationStatusClassification.InterviewStatuses.Contains(x.ToStatus)))
            {
                interviewCount++;
            }

            if (group.Any(x => ApplicationStatusClassification.OfferStatuses.Contains(x.ToStatus)))
            {
                offerCount++;
            }
        }

        var ghostedCount = applications.Count(a => a.Status == ApplicationStatus.Ghosted);
        var closureCount = applications.Count(a => CompanyGivenClosureStatuses.Values.Contains(a.Status));

        var responseRate = AnalyticsCalculations.CalculateRate(respondedCount, total);
        var averageResponseTimeDays = AnalyticsCalculations.Average(responseTimeDays);
        var closureRate = AnalyticsCalculations.CalculateRate(closureCount, total);

        var responseTimeScore = CompanyIntelligenceCalculations.CalculateResponseTimeScore(
            averageResponseTimeDays, opts.ResponseTimeCapDays);
        var candidateExperienceScore = CompanyIntelligenceCalculations.CalculateCandidateExperienceScore(
            responseRate, responseTimeScore, closureRate,
            opts.ResponsivenessWeight, opts.ResponseTimeWeight, opts.ClosureRateWeight);

        var metrics = new CompanyIntelligenceMetrics(
            TotalApplications: total,
            ResponseRate: responseRate,
            GhostingRate: AnalyticsCalculations.CalculateRate(ghostedCount, total),
            InterviewRate: AnalyticsCalculations.CalculateRate(interviewCount, total),
            OfferRate: AnalyticsCalculations.CalculateRate(offerCount, total),
            AverageResponseTimeDays: averageResponseTimeDays,
            MedianResponseTimeDays: AnalyticsCalculations.Median(responseTimeDays),
            ClosureRate: closureRate,
            CandidateExperienceScore: candidateExperienceScore);

        return new CompanyIntelligenceResponse(company.Id, company.Name, confidence, metrics);
    }
}
