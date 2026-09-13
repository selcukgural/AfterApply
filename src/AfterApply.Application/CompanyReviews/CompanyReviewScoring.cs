namespace AfterApply.Application.CompanyReviews;

/// <summary>
/// The published company score. Pure, so the public scoring page and the unit tests can quote it
/// exactly:
///
/// <c>score = (n · avg + m · globalAvg) / (n + m)</c>
///
/// where <c>n</c> is the number of approved reviews of the company, <c>avg</c> the mean of their
/// Overall ratings, <c>globalAvg</c> the mean Overall rating across every approved review on the
/// site, and <c>m</c> the prior weight (<c>CompanyReviews:PriorWeight</c>, 5 by default). A
/// company with few reviews is pulled toward the site-wide mean and earns its own number as
/// reviews accumulate; a lone 5-star cannot put a company at the top. Below
/// <c>CompanyReviews:MinimumReviewsForScore</c> approved reviews no score is shown at all.
///
/// Category averages are plain means — they are shown as bars next to the sample size, not
/// ranked, so there is nothing to defend against.
/// </summary>
public static class CompanyReviewScoring
{
    /// <summary>What <c>globalAverage</c> falls back to before any review is approved anywhere.
    /// The midpoint of the scale — the only value that says nothing.</summary>
    public const double NeutralAverage = 3.0;

    public static double? BayesianScore(int approvedCount, double sumOfOverall, double globalAverage,
        int priorWeight, int minimumReviews)
    {
        if (approvedCount <= 0 || approvedCount < minimumReviews)
        {
            return null;
        }

        var average = sumOfOverall / approvedCount;
        var score = (approvedCount * average + priorWeight * globalAverage) / (approvedCount + priorWeight);
        return Math.Round(score, 1);
    }

    public static double? CategoryAverage(int approvedCount, double sum) =>
        approvedCount <= 0 ? null : Math.Round(sum / approvedCount, 1);
}
