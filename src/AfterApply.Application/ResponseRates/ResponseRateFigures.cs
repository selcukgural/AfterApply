namespace AfterApply.Application.ResponseRates;

/// <summary>
/// The figures every response-rate surface reports, computed by <see cref="ResponseRateAggregator"/>.
/// Rates are percentages rounded to one decimal; durations are days.
///
/// Every rate is taken over the <b>mature</b> cohort — applications at least
/// <c>maturityDays</c> old — not over <see cref="TotalApplications"/>. A three-day-old
/// application has not had time to be answered; counting it as "no reply" pushes the response
/// rate down and the ghosting rate up for no fault of the company's. The plan called this the
/// one thing to settle before a number is ever published under a company's name
/// (DEVELOPMENT_PLAN.md K1); this is the settlement, and the method text on the page says so.
/// Reply times are the exception: a reply that has happened is real data however young the
/// application is, so <see cref="AverageFirstReplyDays"/> and <see cref="MedianFirstReplyDays"/>
/// take every answered application in the window.
/// </summary>
/// <param name="DistinctContributors">How many different people the applications came from.</param>
/// <param name="MaxContributorShare">The largest share (0–1) of the applications any one person
/// accounts for. The privacy guard: a company page whose "aggregate" is mostly one person's
/// history is that person's history with a company name on it.</param>
/// <param name="PostInterviewSilenceRate">Of the mature applications that reached an interview,
/// the share now marked Ghosted — the grievance Ekşi and Blind threads single out ("they called
/// me in and then nothing"). Null when nobody reached an interview.</param>
/// <param name="ClosureRate">The share the company gave an explicit outcome (Rejected/Accepted).</param>
public sealed record ResponseRateFigures(
    int TotalApplications,
    int MatureApplications,
    int DistinctContributors,
    double MaxContributorShare,
    double ResponseRate,
    double GhostingRate,
    double InterviewRate,
    double OfferRate,
    double? PostInterviewSilenceRate,
    double? AverageFirstReplyDays,
    double? MedianFirstReplyDays,
    double ClosureRate);
