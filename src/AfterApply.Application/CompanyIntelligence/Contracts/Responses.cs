using AfterApply.Domain.Companies;

namespace AfterApply.Application.CompanyIntelligence.Contracts;

public sealed record CompanyIntelligenceMetrics(
    int TotalApplications,
    double ResponseRate,
    double GhostingRate,
    double InterviewRate,
    double OfferRate,
    double? AverageResponseTimeDays,
    double? MedianResponseTimeDays,
    // % of applications where the company itself gave an explicit outcome (Rejected/Accepted) —
    // see CompanyGivenClosureStatuses. Ghosted and Withdrawn don't count; spec §14 Closure Rate.
    double ClosureRate,
    // Composite of Responsiveness (=ResponseRate) / Response Time / ClosureRate — spec §14.
    // Interview Experience and Process Transparency are not included: no raw data exists yet
    // for either (see DEVELOPMENT_PLAN.md Sprint 11).
    double CandidateExperienceScore);

public sealed record CompanyIntelligenceResponse(
    Guid CompanyId,
    string CompanyName,
    ConfidenceBucket Confidence,
    // Deliberately null when Confidence == Hidden — spec §16 privacy-by-design: a tiny sample
    // could itself deanonymize applicants, so no metric (not even the count) is exposed below
    // the Hidden threshold. See DECISIONS.md "Sprint 10" entry.
    CompanyIntelligenceMetrics? Metrics);
