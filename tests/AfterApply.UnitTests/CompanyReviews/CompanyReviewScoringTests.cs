using AfterApply.Application.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

public class CompanyReviewScoringTests
{
    [Fact]
    public void No_Score_Below_The_Minimum()
    {
        CompanyReviewScoring.BayesianScore(2, sumOfOverall: 10, globalAverage: 3.5, priorWeight: 5, minimumReviews: 3).ShouldBeNull();
        CompanyReviewScoring.BayesianScore(0, 0, 3.5, 5, 3).ShouldBeNull();
    }

    [Fact]
    public void Matches_The_Published_Formula()
    {
        // (n·avg + m·global) / (n + m) with n=4, avg=4.5, m=5, global=3.5
        // = (18 + 17.5) / 9 = 3.944… → 3.9
        CompanyReviewScoring.BayesianScore(4, sumOfOverall: 18, globalAverage: 3.5, priorWeight: 5, minimumReviews: 3).ShouldBe(3.9);
    }

    [Fact]
    public void Three_Five_Stars_Do_Not_Reach_Five()
    {
        // The point of the prior: a company with three perfect reviews scores well, not perfectly,
        // until it has earned its own number.
        var score = CompanyReviewScoring.BayesianScore(3, 15, 3.5, 5, 3)!.Value;

        score.ShouldBeGreaterThan(3.5);
        score.ShouldBeLessThan(5.0);
        score.ShouldBe(4.1);
    }

    [Fact]
    public void With_No_Prior_It_Is_The_Plain_Mean()
    {
        CompanyReviewScoring.BayesianScore(3, 12, 3.5, priorWeight: 0, minimumReviews: 1).ShouldBe(4.0);
    }

    [Fact]
    public void Approaches_The_Plain_Mean_As_Reviews_Accumulate()
    {
        var few = CompanyReviewScoring.BayesianScore(3, 15, 3.0, 5, 3)!.Value;
        var many = CompanyReviewScoring.BayesianScore(300, 1500, 3.0, 5, 3)!.Value;

        (5.0 - many).ShouldBeLessThan(5.0 - few);
        many.ShouldBe(5.0);
    }

    [Fact]
    public void Category_Average_Is_Null_Without_Reviews_And_Rounded_Otherwise()
    {
        CompanyReviewScoring.CategoryAverage(0, 0).ShouldBeNull();
        CompanyReviewScoring.CategoryAverage(3, 11).ShouldBe(3.7);
    }
}
