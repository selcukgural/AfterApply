using AfterApply.Application.ResponseRates.Contracts;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Companies;

namespace AfterApply.Application.CompanyIntelligence.Contracts;

public sealed record CompanyIntelligenceMetrics(
    int TotalApplications,
    // How many of those are old enough to be judged (ResponseRateFigures): every rate below is
    // over this cohort, TotalApplications is the window's whole count.
    int MatureApplications,
    // How many different people the applications came from — printed beside the figure ("12
    // kişinin 64 başvurusu") so a reader can weigh it.
    int DistinctContributors,
    double ResponseRate,
    double GhostingRate,
    double InterviewRate,
    double OfferRate,
    // Of the applications that reached an interview, the share now marked Ghosted; null when
    // none did. The post-interview silence the forums single out as the real grievance.
    double? PostInterviewSilenceRate,
    double? AverageResponseTimeDays,
    double? MedianResponseTimeDays,
    // % of applications where the company itself gave an explicit outcome (Rejected/Accepted) —
    // see CompanyGivenClosureStatuses. Ghosted and Withdrawn don't count; spec §14 Closure Rate.
    double ClosureRate,
    // Composite of Responsiveness (=ResponseRate) / Response Time / ClosureRate — spec §14.
    // Interview Experience and Process Transparency are not included: no raw data exists yet
    // for either (see DEVELOPMENT_PLAN.md Sprint 11).
    double CandidateExperienceScore,
    // Of the settled reply promises, the share kept; of the rejections whose route is known, the
    // share the company told the candidate itself. Self-reported and optional, so each has its own
    // floor (ResponseRateAggregator.SubRateMinimumSamples) and is null below it. Deliberately not
    // in CandidateExperienceScore: a score whose inputs appear and disappear with a floor would
    // move for reasons that have nothing to do with the company.
    double? PromiseKeptRate = null,
    double? RejectionNoticeRate = null);

/// <summary>
/// The company's sector and, when that sector is itself above the public threshold, its figures
/// — the "sector median" the page puts under each of the company's numbers. Present even when
/// the company is Hidden: the sector's aggregate names nobody, and a below-threshold page that
/// still shows the sector is the difference between "nothing here" and "not yet, but here is
/// the wider picture".
/// </summary>
public sealed record CompanySectorComparison(BenchmarkSector Sector, ResponseRateFiguresResponse? Figures);

public sealed record CompanyIntelligenceResponse(
    Guid CompanyId,
    string CompanyName,
    ConfidenceBucket Confidence,
    // The window every figure below is computed over, as concrete dates rather than "last 12
    // months" — a reader should never have to know the server's configuration, or guess when the
    // clock started, to know what a number is a claim about. Present even when Confidence is
    // Hidden: "fewer than the threshold in this period" and "fewer ever" are different statements,
    // and only the first one is true.
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    // Deliberately null when Confidence == Hidden — spec §16 privacy-by-design: a tiny sample
    // could itself deanonymize applicants, so no metric (not even the count) is exposed below
    // the Hidden threshold. See DECISIONS.md "Sprint 10" entry. Hidden also covers the case where
    // one person accounts for more than CompanyIntelligenceOptions.MaxContributorShare of the
    // applications, and the two cases are deliberately indistinguishable.
    CompanyIntelligenceMetrics? Metrics,
    // Null when the company's industry could not be read; see CompanySectorComparison.
    CompanySectorComparison? SectorComparison,
    // Printed, not hard-coded, so the page's method text matches what the server enforces.
    CompanyIntelligenceThresholds Thresholds);

/// <summary>The three rules the "why is nothing shown" card states — the count floor, the age an
/// application must reach before it is judged, and the largest share one person may hold.</summary>
public sealed record CompanyIntelligenceThresholds(int HiddenBelow, int MaturityDays, int MaxContributorSharePercent);
