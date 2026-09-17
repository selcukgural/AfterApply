using AfterApply.Application.CandidateExperiences;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CandidateExperiences;

public class CandidateExperienceStatsTests
{
    private static ExperienceAggregateRow Row(int overall, HiringOutcome? outcome = null, ProcessDuration? duration = null,
        StageCount? stages = null, IReadOnlyList<ExperienceCategoryRating>? ratings = null,
        IReadOnlyList<(string, ReviewStatementKind)>? picks = null, IReadOnlyList<InterviewType>? types = null) =>
        new(overall, outcome, duration, stages, ratings ?? [], picks ?? [], types ?? []);

    [Theory]
    [InlineData(1, "2026-Q1")]
    [InlineData(3, "2026-Q1")]
    [InlineData(4, "2026-Q2")]
    [InlineData(6, "2026-Q2")]
    [InlineData(7, "2026-Q3")]
    [InlineData(9, "2026-Q3")]
    [InlineData(10, "2026-Q4")]
    [InlineData(12, "2026-Q4")]
    public void Quarter_Labels_Each_Month(int month, string expected)
    {
        CandidateExperienceStats.Quarter(new DateTimeOffset(2026, month, 15, 0, 0, 0, TimeSpan.Zero)).ShouldBe(expected);
    }

    [Fact]
    public void Ordinal_Median_Is_The_Middle_Value_And_The_Lower_Middle_On_Even_Counts()
    {
        CandidateExperienceStats.OrdinalMedian<ProcessDuration>([]).ShouldBeNull();
        CandidateExperienceStats.OrdinalMedian([ProcessDuration.OverTwoMonths]).ShouldBe(ProcessDuration.OverTwoMonths);
        CandidateExperienceStats.OrdinalMedian([ProcessDuration.OverTwoMonths, ProcessDuration.UnderOneWeek, ProcessDuration.TwoToFourWeeks])
            .ShouldBe(ProcessDuration.TwoToFourWeeks);
        CandidateExperienceStats.OrdinalMedian([StageCount.Four, StageCount.One, StageCount.Two, StageCount.FivePlus])
            .ShouldBe(StageCount.Two);
    }

    [Fact]
    public void Below_The_Threshold_Only_The_Count_The_Average_And_The_Distribution_Are_Published()
    {
        var summary = CandidateExperienceStats.Build(
            [Row(5, HiringOutcome.Offer, ProcessDuration.UnderOneWeek, StageCount.One,
                [new(ExperienceCategory.Communication, 5)], [("communication.pos.timely_information", ReviewStatementKind.Liked)], [InterviewType.TakeHomeAssignment])],
            globalAverage: 3.0, minimumForStats: 3, priorWeight: 5);

        summary.Count.ShouldBe(1);
        summary.AverageOverall.ShouldBe(5.0);
        summary.Distribution.ShouldBe([0, 0, 0, 0, 1]);
        summary.Score.ShouldBeNull();
        summary.Categories.Single(c => c.Category == ExperienceCategory.Communication).Count.ShouldBe(1);
        summary.Categories.ShouldAllBe(c => c.Average == null);
        summary.TopLiked.ShouldBeEmpty();
        summary.Outcomes.ShouldBeEmpty();
        summary.TypicalDuration.ShouldBeNull();
        summary.TypicalStages.ShouldBeNull();
        summary.InterviewTypes.ShouldBeEmpty();
        summary.TakeHomeAssignmentCount.ShouldBe(0);
    }

    [Fact]
    public void At_The_Threshold_Every_Aggregate_Is_Published()
    {
        var rows = new[]
        {
            Row(4, HiringOutcome.Rejected, ProcessDuration.TwoToFourWeeks, StageCount.Three,
                [new(ExperienceCategory.OutcomeCommunication, 1), new(ExperienceCategory.Communication, 4)],
                [("outcome.imp.notification", ReviewStatementKind.Improve), ("communication.pos.steps_clear_upfront", ReviewStatementKind.Liked)],
                [InterviewType.Video, InterviewType.TakeHomeAssignment]),
            Row(2, HiringOutcome.NoResponse, ProcessDuration.OneToTwoMonths, StageCount.Two,
                [new(ExperienceCategory.OutcomeCommunication, 1)],
                [("outcome.imp.notification", ReviewStatementKind.Improve), ("outcome.imp.notification_time", ReviewStatementKind.Improve)],
                [InterviewType.Video]),
            Row(5, HiringOutcome.Offer, ProcessDuration.OneToTwoWeeks, null,
                [new(ExperienceCategory.OutcomeCommunication, 5), new(ExperienceCategory.Communication, 5)],
                [("communication.pos.steps_clear_upfront", ReviewStatementKind.Liked), ("response.pos.quick_replies", ReviewStatementKind.Liked)],
                [InterviewType.Video, InterviewType.TakeHomeAssignment, InterviewType.Panel]),
        };

        var summary = CandidateExperienceStats.Build(rows, globalAverage: 3.0, minimumForStats: 3, priorWeight: 5);

        summary.Count.ShouldBe(3);
        // (3 · 3.667 + 5 · 3) / 8 = 3.25 → 3.2: one decimal with the review score's banker's rounding.
        summary.Score.ShouldBe(3.2);
        summary.AverageOverall.ShouldBe(3.7);
        summary.Distribution.ShouldBe([0, 1, 0, 1, 1]);
        summary.Categories.Single(c => c.Category == ExperienceCategory.OutcomeCommunication).Average.ShouldBe(2.3);
        // Two votes: under the threshold, count shown, average hidden.
        var communication = summary.Categories.Single(c => c.Category == ExperienceCategory.Communication);
        communication.Count.ShouldBe(2);
        communication.Average.ShouldBeNull();
        summary.TopLiked.Select(s => (s.Key, s.Count)).ShouldBe([("communication.pos.steps_clear_upfront", 2), ("response.pos.quick_replies", 1)]);
        summary.TopImprovable.Select(s => (s.Key, s.Count)).ShouldBe([("outcome.imp.notification", 2), ("outcome.imp.notification_time", 1)]);
        summary.Outcomes.Select(o => (o.Outcome, o.Count)).ShouldBe([(HiringOutcome.Offer, 1), (HiringOutcome.Rejected, 1), (HiringOutcome.NoResponse, 1)]);
        summary.TypicalDuration.ShouldBe(ProcessDuration.TwoToFourWeeks);
        // Two answers: the lower middle.
        summary.TypicalStages.ShouldBe(StageCount.Two);
        summary.InterviewTypes.Select(t => (t.Type, t.Count)).ShouldBe([(InterviewType.Video, 3), (InterviewType.TakeHomeAssignment, 2), (InterviewType.Panel, 1)]);
        summary.TakeHomeAssignmentCount.ShouldBe(2);
    }

    [Fact]
    public void Top_Lists_Break_Ties_By_Key_And_Stop_At_Three()
    {
        var rows = Enumerable.Range(0, 3).Select(_ => Row(3, picks:
        [
            ("general.pos.clear_process", ReviewStatementKind.Liked),
            ("general.pos.respectful_approach", ReviewStatementKind.Liked),
            ("general.pos.positive_process", ReviewStatementKind.Liked),
            ("general.pos.professional_process", ReviewStatementKind.Liked),
        ])).ToArray();

        var summary = CandidateExperienceStats.Build(rows, 3.0, 3, 5);

        summary.TopLiked.Select(s => s.Key).ShouldBe(["general.pos.clear_process", "general.pos.positive_process", "general.pos.professional_process"]);
    }

    [Fact]
    public void Categories_Are_Always_The_Eight_Optional_Ones_In_Order()
    {
        var summary = CandidateExperienceStats.Build([], 3.0, 3, 5);

        summary.Count.ShouldBe(0);
        summary.AverageOverall.ShouldBeNull();
        summary.Categories.Select(c => c.Category)
            .ShouldBe(Enum.GetValues<ExperienceCategory>().Where(c => c != ExperienceCategory.Overall));
    }
}
