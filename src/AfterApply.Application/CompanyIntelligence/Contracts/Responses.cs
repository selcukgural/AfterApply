using AfterApply.Domain.Companies;

namespace AfterApply.Application.CompanyIntelligence.Contracts;

public sealed record CompanyIntelligenceMetrics(
    int TotalApplications,
    double ResponseRate,
    double GhostingRate,
    double InterviewRate,
    double OfferRate,
    double? AverageResponseTimeDays,
    double? MedianResponseTimeDays);

public sealed record CompanyIntelligenceResponse(
    Guid CompanyId,
    string CompanyName,
    ConfidenceBucket Confidence,
    // Deliberately null when Confidence == Hidden — spec §16 privacy-by-design: a tiny sample
    // could itself deanonymize applicants, so no metric (not even the count) is exposed below
    // the Hidden threshold. See DECISIONS.md "Sprint 10" entry.
    CompanyIntelligenceMetrics? Metrics);
