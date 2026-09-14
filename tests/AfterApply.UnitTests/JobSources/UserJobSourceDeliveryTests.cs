using AfterApply.Domain.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class UserJobSourceDeliveryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T04:00:00Z");

    private static UserJobSourceDelivery Delivery() =>
        UserJobSourceDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 202638, 0, Now);

    [Fact]
    public void A_Fresh_Row_Can_Be_Scored_And_A_Scored_One_Cannot()
    {
        var delivery = Delivery();
        delivery.CanBeScored.ShouldBeTrue();

        delivery.SetScore(81, "You fit.", ["C#"], [], ["C#"], Now);

        delivery.Score.ShouldBe(81);
        delivery.ScoredAt.ShouldBe(Now);
        delivery.ScoreAttempts.ShouldBe(1);
        delivery.CanBeScored.ShouldBeFalse();
    }

    [Fact]
    public void Failed_Attempts_Are_Counted_And_Give_Up_At_The_Cap()
    {
        var delivery = Delivery();
        for (var i = 0; i < UserJobSourceDelivery.MaxScoreAttempts; i++)
        {
            delivery.CanBeScored.ShouldBeTrue();
            delivery.RecordScoringAttemptFailed();
        }

        delivery.CanBeScored.ShouldBeFalse();
        delivery.Score.ShouldBeNull();
    }

    [Fact]
    public void The_Row_Bounds_What_It_Stores_Even_If_The_Caller_Did_Not()
    {
        var delivery = Delivery();

        delivery.SetScore(130, new string('s', 1000), Enumerable.Range(0, 12).Select(i => $"m{i}"), ["x", "X", " ", "x"],
            [new string('k', 300)], Now);

        delivery.Score.ShouldBe(100);
        delivery.ScoreSummary!.Length.ShouldBe(UserJobSourceDelivery.MaxSummaryLength);
        delivery.MatchedCriteria.Length.ShouldBe(UserJobSourceDelivery.MaxCriteriaItems);
        delivery.MissingCriteria.ShouldBe(["x"]);
        delivery.RequiredSkills[0].Length.ShouldBe(UserJobSourceDelivery.MaxCriterionLength);
    }
}
